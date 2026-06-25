using CourierMax.Application.DTOs;
using CourierMax.Application.Interfaces;
using CourierMax.Domain.Entities;
using CourierMax.Domain.Enums;
using CourierMax.Domain.Exceptions;
using CourierMax.Domain.Interfaces;
using CourierMax.Domain.ValueObjects;

namespace CourierMax.Application.Services;

/// <summary>
/// Orquesta los casos de uso de Shipment (RF-01, RF-02, RF-03, RF-05).
/// Es deliberadamente un "Application Service" simple (no CQRS/MediatR):
/// para 5-6 casos de uso, un servicio con métodos explícitos es más legible
/// que un Command/Handler/Validator por operación. La lógica de negocio que
/// protege invariantes (transiciones de estado, motivo de cancelación) vive
/// en el agregado Shipment; este servicio coordina repos + servicios externos
/// (pricing, calendario, capacidad) y traduce entre DTOs y dominio.
/// </summary>
public sealed class ShipmentService : IShipmentService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICityRepository _cityRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IRateCalculator _rateCalculator;
    private readonly IBusinessDayCalculator _businessDayCalculator;
    private readonly IVehicleCapacityService _vehicleCapacityService;
    private readonly ITrackingCodeGenerator _trackingCodeGenerator;

    public ShipmentService(
        IShipmentRepository shipmentRepository,
        ICityRepository cityRepository,
        IDriverRepository driverRepository,
        IRateCalculator rateCalculator,
        IBusinessDayCalculator businessDayCalculator,
        IVehicleCapacityService vehicleCapacityService,
        ITrackingCodeGenerator trackingCodeGenerator)
    {
        _shipmentRepository = shipmentRepository;
        _cityRepository = cityRepository;
        _driverRepository = driverRepository;
        _rateCalculator = rateCalculator;
        _businessDayCalculator = businessDayCalculator;
        _vehicleCapacityService = vehicleCapacityService;
        _trackingCodeGenerator = trackingCodeGenerator;
    }

    public async Task<ShipmentResponse> CreateAsync(CreateShipmentRequest request)
    {
        var originCity = await _cityRepository.GetByNameAsync(request.OriginCity)
            ?? throw new InvalidCityRouteException(request.OriginCity, request.DestinationCity);
        var destinationCity = await _cityRepository.GetByNameAsync(request.DestinationCity)
            ?? throw new InvalidCityRouteException(request.OriginCity, request.DestinationCity);

        var distance = await _cityRepository.GetDistanceAsync(originCity.Id, destinationCity.Id)
            ?? throw new InvalidCityRouteException(request.OriginCity, request.DestinationCity);

        var sender = new ContactInfo(request.SenderName, request.SenderPhone, request.SenderAddress);
        var recipient = new ContactInfo(request.RecipientName, request.RecipientPhone, request.RecipientAddress);
        var package = new PackageInfo(
            request.PackageWeightKg, request.PackageLengthCm, request.PackageWidthCm, request.PackageHeightCm,
            request.PackageType);

        var cost = _rateCalculator.Calculate(request.ServiceType, package.WeightKg, package.Type, distance.DistanceFee);

        var now = DateTime.UtcNow;
        var slaDeadline = _businessDayCalculator.CalculateSlaDeadline(now, request.ServiceType);
        var trackingCode = await _trackingCodeGenerator.GenerateUniqueAsync();

        var shipment = Shipment.Create(
            trackingCode, sender, recipient, package, request.ServiceType,
            originCity.Id, destinationCity.Id, cost, slaDeadline, now);

        await _shipmentRepository.AddAsync(shipment);
        await _shipmentRepository.SaveChangesAsync();

        return await MapToResponseAsync(shipment);
    }

    public async Task<ShipmentResponse> GetByTrackingCodeAsync(string trackingCode)
    {
        var shipment = await _shipmentRepository.GetByTrackingCodeAsync(trackingCode)
            ?? throw new ShipmentNotFoundException(trackingCode);

        return await MapToResponseAsync(shipment);
    }

    public async Task<List<ShipmentResponse>> ListAsync(ShipmentListFilter filter)
    {
        int? originCityId = null;
        int? destinationCityId = null;

        if (!string.IsNullOrWhiteSpace(filter.OriginCity))
            originCityId = (await _cityRepository.GetByNameAsync(filter.OriginCity))?.Id;

        if (!string.IsNullOrWhiteSpace(filter.DestinationCity))
            destinationCityId = (await _cityRepository.GetByNameAsync(filter.DestinationCity))?.Id;

        var shipments = await _shipmentRepository.ListAsync(filter.Status, originCityId, destinationCityId);

        var responses = new List<ShipmentResponse>();
        foreach (var shipment in shipments)
            responses.Add(await MapToResponseAsync(shipment));

        return responses;
    }

    public async Task<List<ShipmentResponse>> GetDelayedAsync(DateOnly fromDate, DateOnly toDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shipments = await _shipmentRepository.GetDelayedAsync(fromDate, toDate, today);

        var responses = new List<ShipmentResponse>();
        foreach (var shipment in shipments)
            responses.Add(await MapToResponseAsync(shipment));

        return responses;
    }

    public async Task<ShipmentResponse> ChangeStatusAsync(int shipmentId, ChangeStatusRequest request)
    {
        var shipment = await _shipmentRepository.GetByIdAsync(shipmentId)
            ?? throw new ShipmentNotFoundException(shipmentId.ToString());

        var now = DateTime.UtcNow;
        var wasAssignedVehicleId = shipment.AssignedVehicleId;

        switch (request.NewStatus)
        {
            case ShipmentStatus.EnTransito:
                shipment.MarkInTransit(request.ChangedByUserId, now);
                break;
            case ShipmentStatus.Entregado:
                shipment.MarkDelivered(request.ChangedByUserId, now);
                break;
            case ShipmentStatus.Cancelado:
                shipment.Cancel(request.Reason ?? string.Empty, request.ChangedByUserId, now);
                // RN-03: al cancelar, se libera la capacidad del vehículo. Como la carga
                // se calcula en tiempo real a partir de envíos activos (ver Vehicle.cs),
                // "liberar" ocurre automáticamente: este envío ya no cuenta como activo
                // en futuras consultas de GetActiveLoadForVehicleAsync. No se requiere
                // acción adicional aquí, más allá de que el estado ya cambió a Cancelado.
                _ = wasAssignedVehicleId; // documentación de la decisión, sin efecto runtime
                break;
            case ShipmentStatus.Asignado:
                throw new InvalidStatusTransitionException(shipment.Status.ToString(), request.NewStatus.ToString());
            case ShipmentStatus.Creado:
                throw new InvalidStatusTransitionException(shipment.Status.ToString(), request.NewStatus.ToString());
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }

        await _shipmentRepository.SaveChangesAsync();
        return await MapToResponseAsync(shipment);
    }

    public async Task<ShipmentResponse> AssignAsync(int shipmentId, AssignShipmentRequest request)
    {
        var shipment = await _shipmentRepository.GetByIdAsync(shipmentId)
            ?? throw new ShipmentNotFoundException(shipmentId.ToString());

        var driver = await _driverRepository.GetByIdAsync(request.DriverId)
            ?? throw new InvalidOperationException($"Conductor {request.DriverId} no encontrado.");

        driver.EnsureActive(); // RF-03: solo conductores activos

        if (driver.VehicleId is null)
            throw new InvalidOperationException($"El conductor {driver.Name} no tiene un vehículo asignado.");

        var vehicleId = driver.VehicleId.Value;

        // RN-01: valida capacidad ANTES de asignar (vehículo no debe exceder peso/volumen).
        await _vehicleCapacityService.EnsureCapacityAsync(
            vehicleId, shipment.Package.WeightKg, shipment.Package.VolumeM3);

        shipment.AssignTo(driver.Id, vehicleId, "system", DateTime.UtcNow);

        await _shipmentRepository.SaveChangesAsync();
        return await MapToResponseAsync(shipment);
    }

    private async Task<ShipmentResponse> MapToResponseAsync(Shipment shipment)
    {
        var originCity = await _cityRepository.GetByIdAsync(shipment.OriginCityId);
        var destinationCity = await _cityRepository.GetByIdAsync(shipment.DestinationCityId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new ShipmentResponse(
            shipment.Id,
            shipment.TrackingCode,
            shipment.Status,
            shipment.Cost,
            shipment.SlaDeadline,
            shipment.IsDelayed(today),
            shipment.CreatedAt,
            new ContactInfoResponse(shipment.Sender.Name, shipment.Sender.Phone, shipment.Sender.Address),
            new ContactInfoResponse(shipment.Recipient.Name, shipment.Recipient.Phone, shipment.Recipient.Address),
            new PackageInfoResponse(
                shipment.Package.WeightKg, shipment.Package.LengthCm, shipment.Package.WidthCm,
                shipment.Package.HeightCm, shipment.Package.Type),
            shipment.ServiceType,
            originCity?.Name ?? string.Empty,
            destinationCity?.Name ?? string.Empty,
            shipment.AssignedDriverId,
            shipment.AssignedVehicleId,
            shipment.StatusHistory
                .Select(sc => new StatusChangeResponse(sc.FromStatus, sc.ToStatus, sc.ChangedAt, sc.Reason, sc.ChangedByUserId))
                .ToList());
    }
}
