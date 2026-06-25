using CourierMax.Application.DTOs;
using CourierMax.Application.Interfaces;
using CourierMax.Domain.Interfaces;

namespace CourierMax.Application.Services;

/// <summary>
/// RF-06: reporte de métricas por conductor. Optimizado para empujar los
/// conteos y la suma de peso a SQL (COUNT/SUM vía IShipmentRepository.GetDriverAggregatesAsync)
/// en lugar de cargar todos los envíos del conductor completos en memoria.
/// Solo el cálculo de promedio de días de entrega y % de cumplimiento de SLA
/// se hace en memoria, sobre una proyección mínima (fechas, no entidades completas)
/// porque mezcla TimeSpan y comparación de DateOnly de forma que no es práctico
/// expresar como una sola query SQL portable entre SQLite/SQL Server.
///
/// Sigue sin usar CQRS/handler dedicado: el reporte es de solo lectura y sin
/// reglas de negocio complejas, así que un servicio simple es proporcional
/// a su importancia relativa frente a RF-01..RF-05.
/// </summary>
public sealed class DriverReportService : IDriverReportService
{
    private readonly IDriverRepository _driverRepository;
    private readonly IShipmentRepository _shipmentRepository;

    public DriverReportService(IDriverRepository driverRepository, IShipmentRepository shipmentRepository)
    {
        _driverRepository = driverRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<DriverReportResponse> GenerateReportAsync(int driverId)
    {
        var driver = await _driverRepository.GetByIdAsync(driverId)
            ?? throw new InvalidOperationException($"Conductor {driverId} no encontrado.");

        var aggregates = await _shipmentRepository.GetDriverAggregatesAsync(driverId);

        var avgDeliveryDays = 0.0;
        var slaCompliancePct = 0.0;

        if (aggregates.DeliveryProjections.Count > 0)
        {
            var deliveryDurations = aggregates.DeliveryProjections
                .Select(p => (p.DeliveredAt - (p.AssignedAt ?? p.CreatedAt)).TotalDays)
                .ToList();

            avgDeliveryDays = deliveryDurations.Average();

            var withinSla = aggregates.DeliveryProjections
                .Count(p => DateOnly.FromDateTime(p.DeliveredAt) <= p.SlaDeadline);

            slaCompliancePct = (double)withinSla / aggregates.DeliveryProjections.Count * 100;
        }

        return new DriverReportResponse(
            driver.Id,
            driver.Name,
            aggregates.TotalAssigned,
            aggregates.TotalDelivered,
            aggregates.TotalCancelled,
            aggregates.TotalInTransit,
            Math.Round(avgDeliveryDays, 2),
            Math.Round(slaCompliancePct, 2),
            aggregates.TotalWeightKg);
    }
}

public sealed class VehicleQueryService : IVehicleQueryService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IShipmentRepository _shipmentRepository;

    public VehicleQueryService(IVehicleRepository vehicleRepository, IShipmentRepository shipmentRepository)
    {
        _vehicleRepository = vehicleRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<List<VehicleResponse>> ListWithLoadAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var result = new List<VehicleResponse>();

        foreach (var vehicle in vehicles)
        {
            var (currentWeight, currentVolume) = await _shipmentRepository.GetActiveLoadForVehicleAsync(vehicle.Id);
            result.Add(new VehicleResponse(
                vehicle.Id, vehicle.Plate, vehicle.MaxWeightKg, vehicle.MaxVolumeM3, currentWeight, currentVolume));
        }

        return result;
    }
}
