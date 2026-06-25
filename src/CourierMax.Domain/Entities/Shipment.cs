using CourierMax.Domain.Enums;
using CourierMax.Domain.Exceptions;
using CourierMax.Domain.ValueObjects;

namespace CourierMax.Domain.Entities;

/// <summary>
/// Agregado raíz central del dominio. Encapsula el ciclo de vida completo
/// de un envío: creación, transición de estados (con historial de auditoría),
/// asignación a conductor/vehículo y costo.
///
/// Decisión de diseño: la máquina de estados (RF-02) vive AQUÍ, como método
/// de la entidad, no en el servicio de aplicación. Esto es DDD básico:
/// una regla que protege la consistencia del propio agregado (no se puede
/// pasar de CREADO a ENTREGADO directamente) es responsabilidad del dominio,
/// no de un service que podría olvidarse de validarla.
/// </summary>
public sealed class Shipment
{
    private readonly List<StatusChange> _statusHistory = new();

    public int Id { get; private set; }
    public string TrackingCode { get; private set; } = string.Empty;

    public ContactInfo Sender { get; private set; } = null!;
    public ContactInfo Recipient { get; private set; } = null!;
    public PackageInfo Package { get; private set; } = null!;

    public ServiceType ServiceType { get; private set; }
    public int OriginCityId { get; private set; }
    public int DestinationCityId { get; private set; }

    public ShipmentStatus Status { get; private set; }
    public decimal Cost { get; private set; }

    public int? AssignedDriverId { get; private set; }
    public int? AssignedVehicleId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateOnly SlaDeadline { get; private set; }

    public IReadOnlyCollection<StatusChange> StatusHistory => _statusHistory.AsReadOnly();

    private Shipment() { } // EF Core

    /// <summary>
    /// Fábrica del agregado. El costo y el SLA deadline se calculan ANTES de
    /// construir el envío (inyectados como parámetros ya resueltos) porque son
    /// el resultado de políticas externas (tarifas, calendario de festivos) que
    /// no son responsabilidad de esta entidad — Shipment solo orquesta su propio
    /// estado, no recalcula reglas de pricing o calendario.
    /// </summary>
    public static Shipment Create(
        string trackingCode,
        ContactInfo sender,
        ContactInfo recipient,
        PackageInfo package,
        ServiceType serviceType,
        int originCityId,
        int destinationCityId,
        decimal cost,
        DateOnly slaDeadline,
        DateTime createdAt)
    {
        _ = new TrackingCode(trackingCode); // valida formato, lanza si es inválido

        var shipment = new Shipment
        {
            TrackingCode = trackingCode,
            Sender = sender,
            Recipient = recipient,
            Package = package,
            ServiceType = serviceType,
            OriginCityId = originCityId,
            DestinationCityId = destinationCityId,
            Cost = cost,
            SlaDeadline = slaDeadline,
            CreatedAt = createdAt,
            Status = ShipmentStatus.Creado
        };

        shipment._statusHistory.Add(new StatusChange(
            fromStatus: null,
            toStatus: ShipmentStatus.Creado,
            changedByUserId: "system",
            reason: null,
            changedAt: createdAt));

        return shipment;
    }

    /// <summary>
    /// Asigna el envío a un conductor y vehículo. La validación de capacidad
    /// del vehículo (RN-01) se hace ANTES de llamar este método, en
    /// IVehicleCapacityService, porque requiere conocer la carga actual de
    /// TODOS los envíos del vehículo (no es información que Shipment posea
    /// por sí mismo). Aquí solo se aplica la transición de estado.
    /// </summary>
    public void AssignTo(int driverId, int vehicleId, string changedByUserId, DateTime changedAt)
    {
        EnsureTransitionAllowed(ShipmentStatus.Asignado);

        AssignedDriverId = driverId;
        AssignedVehicleId = vehicleId;
        ApplyStatusChange(ShipmentStatus.Asignado, changedByUserId, reason: null, changedAt);
    }

    public void MarkInTransit(string changedByUserId, DateTime changedAt)
    {
        EnsureTransitionAllowed(ShipmentStatus.EnTransito);
        ApplyStatusChange(ShipmentStatus.EnTransito, changedByUserId, reason: null, changedAt);
    }

    public void MarkDelivered(string changedByUserId, DateTime changedAt)
    {
        EnsureTransitionAllowed(ShipmentStatus.Entregado);
        ApplyStatusChange(ShipmentStatus.Entregado, changedByUserId, reason: null, changedAt);
    }

    /// <summary>
    /// Cancela el envío. Requiere motivo de al menos 5 caracteres (RN-03).
    /// No libera la capacidad del vehículo aquí: eso lo hace el caller
    /// (Application layer) porque la liberación es una consecuencia que
    /// involucra al agregado Vehicle, fuera del boundary de Shipment.
    /// </summary>
    public void Cancel(string reason, string changedByUserId, DateTime changedAt)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new CancellationReasonRequiredException();

        EnsureTransitionAllowed(ShipmentStatus.Cancelado);
        ApplyStatusChange(ShipmentStatus.Cancelado, changedByUserId, reason, changedAt);
    }

    /// <summary>
    /// Determina si el envío está atrasado a una fecha dada (RF-05):
    /// no ha llegado a ENTREGADO y ya pasó su SlaDeadline.
    /// </summary>
    public bool IsDelayed(DateOnly asOfDate)
    {
        return Status != ShipmentStatus.Entregado
            && Status != ShipmentStatus.Cancelado
            && asOfDate > SlaDeadline;
    }

    private void EnsureTransitionAllowed(ShipmentStatus target)
    {
        var allowed = IsTransitionAllowed(Status, target);
        if (!allowed)
            throw new InvalidStatusTransitionException(Status.ToString(), target.ToString());
    }

    /// <summary>
    /// Tabla de transiciones válidas (RF-02):
    /// CREADO → ASIGNADO → EN_TRANSITO → ENTREGADO (lineal, sin saltos)
    /// CANCELADO alcanzable desde cualquier estado excepto ENTREGADO.
    /// </summary>
    private static bool IsTransitionAllowed(ShipmentStatus from, ShipmentStatus to)
    {
        if (to == ShipmentStatus.Cancelado)
            return from != ShipmentStatus.Entregado && from != ShipmentStatus.Cancelado;

        return (from, to) switch
        {
            (ShipmentStatus.Creado, ShipmentStatus.Asignado) => true,
            (ShipmentStatus.Asignado, ShipmentStatus.EnTransito) => true,
            (ShipmentStatus.EnTransito, ShipmentStatus.Entregado) => true,
            _ => false
        };
    }

    private void ApplyStatusChange(ShipmentStatus newStatus, string changedByUserId, string? reason, DateTime changedAt)
    {
        var previous = Status;
        Status = newStatus;
        _statusHistory.Add(new StatusChange(previous, newStatus, changedByUserId, reason, changedAt));
    }
}
