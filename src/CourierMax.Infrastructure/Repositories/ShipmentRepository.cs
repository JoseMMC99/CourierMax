using CourierMax.Domain.Entities;
using CourierMax.Domain.Enums;
using CourierMax.Domain.Interfaces;
using CourierMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourierMax.Infrastructure.Repositories;

public sealed class ShipmentRepository : IShipmentRepository
{
    private readonly CourierMaxDbContext _context;

    public ShipmentRepository(CourierMaxDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Shipment shipment)
    {
        await _context.Shipments.AddAsync(shipment);
    }

    public Task<Shipment?> GetByIdAsync(int id)
    {
        return _context.Shipments
            .Include(s => s.StatusHistory)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task<Shipment?> GetByTrackingCodeAsync(string trackingCode)
    {
        return _context.Shipments
            .Include(s => s.StatusHistory)
            .FirstOrDefaultAsync(s => s.TrackingCode == trackingCode);
    }

    public Task<bool> TrackingCodeExistsAsync(string trackingCode)
    {
        return _context.Shipments.AnyAsync(s => s.TrackingCode == trackingCode);
    }

    public async Task<List<Shipment>> ListAsync(ShipmentStatus? status, int? originCityId, int? destinationCityId)
    {
        var query = _context.Shipments.Include(s => s.StatusHistory).AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        if (originCityId.HasValue)
            query = query.Where(s => s.OriginCityId == originCityId.Value);
        if (destinationCityId.HasValue)
            query = query.Where(s => s.DestinationCityId == destinationCityId.Value);

        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<List<Shipment>> GetDelayedAsync(DateOnly fromDate, DateOnly toDate, DateOnly asOfDate)
    {
        // El filtro de rango aplica sobre CreatedAt (fecha de creación del envío),
        // tal como pide RF-05 ("consultar envíos atrasados por rango de fechas").
        var fromDateTime = fromDate.ToDateTime(TimeOnly.MinValue);
        var toDateTime = toDate.ToDateTime(TimeOnly.MaxValue);

        var candidates = await _context.Shipments
            .Include(s => s.StatusHistory)
            .Where(s => s.CreatedAt >= fromDateTime && s.CreatedAt <= toDateTime)
            .Where(s => s.Status != ShipmentStatus.Entregado && s.Status != ShipmentStatus.Cancelado)
            .ToListAsync();

        // IsDelayed() es lógica de dominio (compara contra SlaDeadline); se aplica
        // en memoria tras filtrar en BD por rango y estado, evitando traer toda
        // la tabla pero sin duplicar la regla de negocio en SQL.
        return candidates.Where(s => s.IsDelayed(asOfDate)).ToList();
    }

    /// <summary>
    /// RF-06 optimizado: los conteos (TotalAssigned, TotalDelivered, TotalCancelled,
    /// TotalInTransit) y la suma de peso se calculan con COUNT/SUM ejecutados en SQL
    /// (EF Core traduce estas expresiones LINQ a agregaciones nativas, no a un
    /// ToListAsync seguido de Count/Sum en memoria — cada línea de abajo genera su
    /// propia query agregada contra la base de datos).
    ///
    /// Solo se materializan a memoria las filas estrictamente necesarias para el
    /// cálculo de SLA por entrega (DeliveryProjections): se consulta StatusChanges
    /// directamente —sin Include de Shipment completo— trayendo apenas las fechas
    /// de Asignado/Entregado y el SlaDeadline. Esto evita cargar Sender, Recipient,
    /// Package y el historial completo de auditoría cuando el reporte no los necesita.
    /// </summary>
    public async Task<DriverShipmentAggregates> GetDriverAggregatesAsync(int driverId)
    {
        var baseQuery = _context.Shipments.Where(s => s.AssignedDriverId == driverId);

        var totalAssigned = await baseQuery.CountAsync();
        var totalDelivered = await baseQuery.CountAsync(s => s.Status == ShipmentStatus.Entregado);
        var totalCancelled = await baseQuery.CountAsync(s => s.Status == ShipmentStatus.Cancelado);
        var totalInTransit = await baseQuery.CountAsync(s => s.Status == ShipmentStatus.EnTransito);
        var totalWeightKg = (await baseQuery
                            .Select(s => s.Package.WeightKg)
                            .ToListAsync())
                            .Sum();

        // Proyección mínima para envíos entregados: se necesita CreatedAt y SlaDeadline
        // (de Shipments), más las fechas de Asignado/Entregado (de StatusChanges).
        // Se trae el historial de SOLO los envíos entregados de este conductor —no de
        // todos sus envíos— y solo los campos de fecha, no el objeto completo.
        var deliveredShipments = await baseQuery
            .Where(s => s.Status == ShipmentStatus.Entregado)
            .Select(s => new { s.Id, s.CreatedAt, s.SlaDeadline })
            .ToListAsync();

        if (deliveredShipments.Count == 0)
        {
            return new DriverShipmentAggregates(
                totalAssigned, totalDelivered, totalCancelled, totalInTransit,
                totalWeightKg, new List<DriverDeliveryProjection>());
        }

        var deliveredIds = deliveredShipments.Select(s => s.Id).ToList();

        // Una sola consulta agrupada por ShipmentId en lugar de N consultas individuales:
        // trae únicamente los cambios a Asignado/Entregado de los envíos entregados de
        // este conductor, agrupados en memoria (el agrupamiento por diccionario es sobre
        // un conjunto ya filtrado y pequeño, no sobre toda la tabla de auditoría).
        var relevantChanges = await _context.StatusChanges
            .Where(sc => deliveredIds.Contains(sc.ShipmentId))
            .Where(sc => sc.ToStatus == ShipmentStatus.Asignado || sc.ToStatus == ShipmentStatus.Entregado)
            .Select(sc => new { sc.ShipmentId, sc.ToStatus, sc.ChangedAt })
            .ToListAsync();

        var changesByShipment = relevantChanges.GroupBy(c => c.ShipmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deliveryProjections = new List<DriverDeliveryProjection>();

        foreach (var shipment in deliveredShipments)
        {
            // TryGetValue (no el indexador []) por resiliencia: si por una
            // inconsistencia de datos un envío Entregado no tuviera su StatusChange
            // de auditoría, se prefiere omitirlo del cálculo de SLA en lugar de que
            // todo el reporte falle con una excepción no controlada.
            if (!changesByShipment.TryGetValue(shipment.Id, out var changes))
                continue;

            var deliveredChange = changes.FirstOrDefault(c => c.ToStatus == ShipmentStatus.Entregado);
            if (deliveredChange is null)
                continue;

            var assignedAt = changes.FirstOrDefault(c => c.ToStatus == ShipmentStatus.Asignado)?.ChangedAt;

            deliveryProjections.Add(new DriverDeliveryProjection(
                assignedAt, shipment.CreatedAt, deliveredChange.ChangedAt, shipment.SlaDeadline));
        }

        return new DriverShipmentAggregates(
            totalAssigned, totalDelivered, totalCancelled, totalInTransit,
            totalWeightKg, deliveryProjections);
    }

    public async Task<(decimal weightKg, decimal volumeM3)> GetActiveLoadForVehicleAsync(int vehicleId)
    {
        // AsNoTracking es obligatorio acá: Package es un Owned Type (OwnsOne en
        // CourierMaxDbContext), y EF Core no permite proyectar un owned entity
        // de forma aislada (sin su Shipment propietario) en una query CON tracking
        // — lanza "owned entities cannot be tracked without their owner". Como
        // esta query es de solo lectura (solo se suma peso/volumen, nunca se
        // modifica nada), AsNoTracking es además la opción correcta de performance,
        // no solo un parche: evita que EF Core arme el ChangeTracker para datos
        // que nunca se van a actualizar.
        var activeShipments = await _context.Shipments
            .AsNoTracking()
            .Where(s => s.AssignedVehicleId == vehicleId
                && (s.Status == ShipmentStatus.Asignado || s.Status == ShipmentStatus.EnTransito))
            .Select(s => new { s.Package })
            .ToListAsync();

        var totalWeight = activeShipments.Sum(s => s.Package.WeightKg);
        var totalVolume = activeShipments.Sum(s => s.Package.VolumeM3);

        return (totalWeight, totalVolume);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}