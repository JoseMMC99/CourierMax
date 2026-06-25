using CourierMax.Application.DTOs;
using CourierMax.Domain.Entities;
using CourierMax.Domain.Enums;

namespace CourierMax.Application.Interfaces;

public interface IShipmentService
{
    Task<ShipmentResponse> CreateAsync(CreateShipmentRequest request);
    Task<ShipmentResponse> GetByTrackingCodeAsync(string trackingCode);
    Task<List<ShipmentResponse>> ListAsync(ShipmentListFilter filter);
    Task<List<ShipmentResponse>> GetDelayedAsync(DateOnly fromDate, DateOnly toDate);
    Task<ShipmentResponse> ChangeStatusAsync(int shipmentId, ChangeStatusRequest request);
    Task<ShipmentResponse> AssignAsync(int shipmentId, AssignShipmentRequest request);
}

public interface IVehicleQueryService
{
    Task<List<VehicleResponse>> ListWithLoadAsync();
}

public interface IDriverReportService
{
    Task<DriverReportResponse> GenerateReportAsync(int driverId);
}

/// <summary>
/// Calcula el costo de un envío (RF-04). Implementado como Strategy: la tarifa
/// base varía por ServiceType, pero los recargos (peso/distancia/tipo de paquete)
/// son políticas compartidas — NO se modelan como una Strategy por recargo porque
/// no varían por ningún eje que justifique esa indirección (KISS).
/// </summary>
public interface IRateCalculator
{
    decimal Calculate(ServiceType serviceType, decimal weightKg, PackageType packageType, decimal distanceFee);
}

/// <summary>
/// Encapsula el calendario de días hábiles colombiano (RN-02) y el cálculo
/// de SLA / detección de atraso (RF-05).
/// </summary>
public interface IBusinessDayCalculator
{
    DateOnly CalculateSlaDeadline(DateTime createdAt, ServiceType serviceType);
    bool IsBusinessDay(DateOnly date);
}

/// <summary>
/// Valida y resuelve la asignación de vehículo/conductor respetando RN-01
/// (capacidad y balanceo de carga: elige el vehículo con menor carga actual
/// entre los que tengan capacidad suficiente).
/// </summary>
public interface IVehicleCapacityService
{
    Task EnsureCapacityAsync(int vehicleId, decimal additionalWeightKg, decimal additionalVolumeM3);
    Task<Vehicle> SelectAvailableVehicleAsync(decimal requiredWeightKg, decimal requiredVolumeM3);
}

public interface ITrackingCodeGenerator
{
    Task<string> GenerateUniqueAsync();
}
