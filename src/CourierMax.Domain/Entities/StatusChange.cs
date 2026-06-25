using CourierMax.Domain.Enums;

namespace CourierMax.Domain.Entities;

/// <summary>
/// Registro histórico de un cambio de estado. Es inmutable una vez creado
/// (auditoría). Vive dentro del boundary del agregado Shipment: no tiene
/// repositorio propio ni se modifica de forma independiente.
/// </summary>
public sealed class StatusChange
{
    public int Id { get; private set; }
    public int ShipmentId { get; private set; }
    public ShipmentStatus? FromStatus { get; private set; } // null en el registro de creación
    public ShipmentStatus ToStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? Reason { get; private set; }
    public string ChangedByUserId { get; private set; } = string.Empty;

    private StatusChange() { } // EF Core

    internal StatusChange(ShipmentStatus? fromStatus, ShipmentStatus toStatus, string changedByUserId,
        string? reason, DateTime changedAt)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
        Reason = reason;
        ChangedAt = changedAt;
    }
}
