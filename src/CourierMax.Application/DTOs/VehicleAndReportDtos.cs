namespace CourierMax.Application.DTOs;

public sealed record VehicleResponse(
    int Id,
    string Plate,
    decimal MaxWeightKg,
    decimal MaxVolumeM3,
    decimal CurrentWeightKg,
    decimal CurrentVolumeM3);

public sealed record DriverReportResponse(
    int DriverId,
    string DriverName,
    int TotalAssigned,
    int TotalDelivered,
    int TotalCancelled,
    int TotalInTransit,
    double AverageDeliveryDays,
    double SlaCompliancePercentage,
    decimal TotalWeightKgTransported);

public sealed record CityResponse(int Id, string Name);
