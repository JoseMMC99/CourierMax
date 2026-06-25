using CourierMax.Domain.Entities;
using CourierMax.Domain.Enums;

namespace CourierMax.Domain.Interfaces;

public interface IShipmentRepository
{
    Task AddAsync(Shipment shipment);
    Task<Shipment?> GetByIdAsync(int id);
    Task<Shipment?> GetByTrackingCodeAsync(string trackingCode);
    Task<bool> TrackingCodeExistsAsync(string trackingCode);
    Task<List<Shipment>> ListAsync(ShipmentStatus? status, int? originCityId, int? destinationCityId);
    Task<List<Shipment>> GetDelayedAsync(DateOnly fromDate, DateOnly toDate, DateOnly asOfDate);
    /// <summary>
    /// Proyección agregada para RF-06, calculada en SQL (no en memoria).
    /// Evita traer la entidad completa con su StatusHistory cuando solo se
    /// necesitan conteos y sumas: COUNT/SUM se empujan a la base de datos
    /// vía LINQ-to-SQL (Sum, Count, Where sobre IQueryable sin materializar).
    /// Solo se trae a memoria lo estrictamente necesario para el cálculo de
    /// SLA por entrega (que requiere comparar la fecha de entrega contra
    /// SlaDeadline por envío) — ver DriverDeliveryProjection.
    /// </summary>
    Task<DriverShipmentAggregates> GetDriverAggregatesAsync(int driverId);

    /// <summary>
    /// Suma el peso/volumen de los envíos actualmente activos (Asignado o EnTransito)
    /// para un vehículo dado. Usado por IVehicleCapacityService para validar RN-01
    /// sin cachear un contador en la entidad Vehicle (ver justificación en Vehicle.cs).
    /// </summary>
    Task<(decimal weightKg, decimal volumeM3)> GetActiveLoadForVehicleAsync(int vehicleId);

    Task SaveChangesAsync();
}

/// <summary>
/// Resultado agregado para el reporte de un conductor (RF-06). Los contadores
/// (TotalAssigned, TotalDelivered, etc.) y la suma de peso se calculan con
/// COUNT/SUM en SQL. DeliveryProjections trae solo lo mínimo por envío entregado
/// (fecha de asignación, fecha de entrega, SlaDeadline) para calcular en memoria
/// el promedio de días y el % de cumplimiento de SLA — cálculos que mezclan
/// múltiples columnas con lógica de negocio (TimeSpan, comparación de DateOnly)
/// que no es práctico ni más legible expresar como una sola query SQL.
/// </summary>
public sealed record DriverShipmentAggregates(
    int TotalAssigned,
    int TotalDelivered,
    int TotalCancelled,
    int TotalInTransit,
    decimal TotalWeightKg,
    List<DriverDeliveryProjection> DeliveryProjections);

public sealed record DriverDeliveryProjection(
    DateTime? AssignedAt,
    DateTime CreatedAt,
    DateTime DeliveredAt,
    DateOnly SlaDeadline);

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(int id);
    Task<List<Vehicle>> GetAllAsync();
}

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(int id);
    Task<List<Driver>> GetAllAsync();
}

public interface ICityRepository
{
    Task<City?> GetByIdAsync(int id);
    Task<City?> GetByNameAsync(string name);
    Task<List<City>> GetAllAsync();
    Task<CityDistance?> GetDistanceAsync(int originCityId, int destinationCityId);
}

public interface IPublicHolidayRepository
{
    Task<HashSet<DateOnly>> GetAllAsync();
}
