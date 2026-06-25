using CourierMax.Application.Interfaces;
using CourierMax.Domain.Entities;
using CourierMax.Domain.Exceptions;
using CourierMax.Domain.Interfaces;

namespace CourierMax.Application.Services;

/// <summary>
/// Implementa RN-01: validación de capacidad y balanceo de carga.
/// La selección automática de vehículo (usada cuando el caller no especifica
/// uno explícito) elige, entre los vehículos con capacidad suficiente, el que
/// tenga MENOR carga actual — balanceo simple y determinista, suficiente para
/// el alcance de esta prueba (sin necesidad de un algoritmo de optimización).
/// </summary>
public sealed class VehicleCapacityService : IVehicleCapacityService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IShipmentRepository _shipmentRepository;

    public VehicleCapacityService(IVehicleRepository vehicleRepository, IShipmentRepository shipmentRepository)
    {
        _vehicleRepository = vehicleRepository;
        _shipmentRepository = shipmentRepository;
    }

    public async Task EnsureCapacityAsync(int vehicleId, decimal additionalWeightKg, decimal additionalVolumeM3)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId)
            ?? throw new InvalidOperationException($"Vehículo {vehicleId} no encontrado.");

        var (currentWeight, currentVolume) = await _shipmentRepository.GetActiveLoadForVehicleAsync(vehicleId);

        vehicle.EnsureCapacity(currentWeight, currentVolume, additionalWeightKg, additionalVolumeM3);
    }

    public async Task<Vehicle> SelectAvailableVehicleAsync(decimal requiredWeightKg, decimal requiredVolumeM3)
    {
        var vehicles = await _vehicleRepository.GetAllAsync();

        var candidates = new List<(Vehicle Vehicle, decimal CurrentWeight, decimal CurrentVolume)>();

        foreach (var vehicle in vehicles)
        {
            var (currentWeight, currentVolume) = await _shipmentRepository.GetActiveLoadForVehicleAsync(vehicle.Id);
            var fitsWeight = currentWeight + requiredWeightKg <= vehicle.MaxWeightKg;
            var fitsVolume = currentVolume + requiredVolumeM3 <= vehicle.MaxVolumeM3;

            if (fitsWeight && fitsVolume)
                candidates.Add((vehicle, currentWeight, currentVolume));
        }

        if (candidates.Count == 0)
            throw new NoAvailableVehicleException(requiredWeightKg, requiredVolumeM3);

        // Balanceo de carga: el de menor carga actual de peso gana.
        return candidates.OrderBy(c => c.CurrentWeight).First().Vehicle;
    }
}
