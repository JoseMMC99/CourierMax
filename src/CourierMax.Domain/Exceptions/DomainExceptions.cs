namespace CourierMax.Domain.Exceptions;

/// <summary>
/// Excepción base para todas las violaciones de reglas de negocio del dominio.
/// La capa Api la mapea a HTTP 409 Conflict de forma centralizada (ver ExceptionMiddleware).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string fromStatus, string toStatus)
        : base($"No se puede cambiar el estado de '{fromStatus}' a '{toStatus}'. " +
               "Transiciones válidas: CREADO→ASIGNADO→EN_TRANSITO→ENTREGADO, " +
               "o CANCELADO desde cualquier estado excepto ENTREGADO.")
    {
    }
}

public sealed class VehicleCapacityExceededException : DomainException
{
    public VehicleCapacityExceededException(string plate, decimal requiredWeight, decimal maxWeight,
        decimal requiredVolume, decimal maxVolume)
        : base($"El vehículo '{plate}' excede su capacidad. " +
               $"Peso requerido: {requiredWeight}kg (máx {maxWeight}kg). " +
               $"Volumen requerido: {requiredVolume}m³ (máx {maxVolume}m³).")
    {
    }
}

public sealed class NoAvailableVehicleException : DomainException
{
    public NoAvailableVehicleException(decimal weightKg, decimal volumeM3)
        : base($"No hay vehículos disponibles con capacidad suficiente para " +
               $"{weightKg}kg / {volumeM3}m³.")
    {
    }
}

public sealed class InactiveDriverException : DomainException
{
    public InactiveDriverException(int driverId)
        : base($"El conductor con Id {driverId} no está activo y no puede recibir asignaciones.")
    {
    }
}

public sealed class CancellationReasonRequiredException : DomainException
{
    public CancellationReasonRequiredException()
        : base("La cancelación de un envío requiere un motivo de al menos 5 caracteres.")
    {
    }
}

public sealed class InvalidCityRouteException : DomainException
{
    public InvalidCityRouteException(string origin, string destination)
        : base($"No existe una ruta tarifada entre '{origin}' y '{destination}', " +
               "o las ciudades no son válidas en el sistema.")
    {
    }
}

public sealed class DuplicateTrackingCodeException : DomainException
{
    public DuplicateTrackingCodeException(string code)
        : base($"El código de rastreo '{code}' ya existe en el sistema.")
    {
    }
}

public sealed class ShipmentNotFoundException : DomainException
{
    public ShipmentNotFoundException(string identifier)
        : base($"No se encontró el envío '{identifier}'.")
    {
    }
}
