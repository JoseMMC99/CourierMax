using CourierMax.Domain.Enums;

namespace CourierMax.Application.DTOs;

public sealed record CreateShipmentRequest(
    string SenderName,
    string SenderPhone,
    string SenderAddress,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    decimal PackageWeightKg,
    decimal PackageLengthCm,
    decimal PackageWidthCm,
    decimal PackageHeightCm,
    PackageType PackageType,
    ServiceType ServiceType,
    string OriginCity,
    string DestinationCity);

public sealed record ContactInfoResponse(string Name, string Phone, string Address);

public sealed record PackageInfoResponse(
    decimal WeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm, PackageType Type);

public sealed record StatusChangeResponse(
    ShipmentStatus? FromStatus, ShipmentStatus ToStatus, DateTime ChangedAt, string? Reason, string ChangedByUserId);

public sealed record ShipmentResponse(
    int Id,
    string TrackingCode,
    ShipmentStatus Status,
    decimal Cost,
    DateOnly SlaDeadline,
    bool IsDelayed,
    DateTime CreatedAt,
    ContactInfoResponse Sender,
    ContactInfoResponse Recipient,
    PackageInfoResponse Package,
    ServiceType ServiceType,
    string OriginCity,
    string DestinationCity,
    int? AssignedDriverId,
    int? AssignedVehicleId,
    List<StatusChangeResponse> StatusHistory);

public sealed record ChangeStatusRequest(
    ShipmentStatus NewStatus,
    string ChangedByUserId,
    string? Reason);

public sealed record AssignShipmentRequest(int DriverId);

public sealed record ShipmentListFilter(ShipmentStatus? Status, string? OriginCity, string? DestinationCity);
