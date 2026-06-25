using CourierMax.Domain.Exceptions;

namespace CourierMax.Domain.Entities;

/// <summary>
/// Vehículo de la flota. NO almacena su carga actual como campo cacheado:
/// se calcula en tiempo real sumando los envíos activos (Asignado/EnTransito)
/// asignados a este vehículo (ver IVehicleRepository.GetCurrentLoadAsync).
/// Esto evita inconsistencias por actualizar contadores en múltiples lugares
/// (al asignar, cancelar, entregar) — el costo de la query es aceptable
/// para el volumen de esta prueba técnica.
/// </summary>
public sealed class Vehicle
{
    public int Id { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public decimal MaxWeightKg { get; private set; }
    public decimal MaxVolumeM3 { get; private set; }

    private Vehicle() { } // EF Core

    public Vehicle(string plate, decimal maxWeightKg, decimal maxVolumeM3)
    {
        if (string.IsNullOrWhiteSpace(plate))
            throw new ArgumentException("La placa no puede estar vacía.", nameof(plate));
        if (maxWeightKg <= 0 || maxVolumeM3 <= 0)
            throw new ArgumentException("La capacidad del vehículo debe ser positiva.");

        Plate = plate;
        MaxWeightKg = maxWeightKg;
        MaxVolumeM3 = maxVolumeM3;
    }

    /// <summary>
    /// Verifica que sumar la carga adicional no exceda la capacidad máxima.
    /// Lanza VehicleCapacityExceededException si la excede (RN-01).
    /// </summary>
    public void EnsureCapacity(decimal currentWeightKg, decimal currentVolumeM3,
        decimal additionalWeightKg, decimal additionalVolumeM3)
    {
        var newWeight = currentWeightKg + additionalWeightKg;
        var newVolume = currentVolumeM3 + additionalVolumeM3;

        if (newWeight > MaxWeightKg || newVolume > MaxVolumeM3)
        {
            throw new VehicleCapacityExceededException(Plate, newWeight, MaxWeightKg, newVolume, MaxVolumeM3);
        }
    }
}
