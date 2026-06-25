using CourierMax.Application.Services;
using CourierMax.Domain.Entities;
using CourierMax.Domain.Exceptions;
using CourierMax.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CourierMax.Application.Tests;

public sealed class VehicleCapacityServiceTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock = new();
    private readonly Mock<IShipmentRepository> _shipmentRepoMock = new();

    private VehicleCapacityService CreateSut() =>
        new(_vehicleRepoMock.Object, _shipmentRepoMock.Object);

    [Fact]
    public async Task EnsureCapacityAsync_VehiculoSinCargaSuficiente_LanzaVehicleCapacityExceededException()
    {
        var vehicle = new Vehicle("DEF456", maxWeightKg: 300, maxVolumeM3: 6);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(vehicle);
        _shipmentRepoMock.Setup(r => r.GetActiveLoadForVehicleAsync(1)).ReturnsAsync((290m, 1m));

        var sut = CreateSut();
        var act = async () => await sut.EnsureCapacityAsync(1, additionalWeightKg: 20, additionalVolumeM3: 0.5m);

        await act.Should().ThrowAsync<VehicleCapacityExceededException>();
    }

    [Fact]
    public async Task SelectAvailableVehicleAsync_VariosDisponibles_EligeElDeMenorCargaActual()
    {
        var vehicleA = new Vehicle("ABC123", 500, 10);
        var vehicleB = new Vehicle("GHI789", 800, 15);
        SetVehicleId(vehicleA, 1);
        SetVehicleId(vehicleB, 2);

        _vehicleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Vehicle> { vehicleA, vehicleB });
        _shipmentRepoMock.Setup(r => r.GetActiveLoadForVehicleAsync(1)).ReturnsAsync((400m, 8m)); // más cargado
        _shipmentRepoMock.Setup(r => r.GetActiveLoadForVehicleAsync(2)).ReturnsAsync((100m, 2m)); // menos cargado

        var sut = CreateSut();
        var selected = await sut.SelectAvailableVehicleAsync(requiredWeightKg: 50, requiredVolumeM3: 1);

        selected.Plate.Should().Be("GHI789"); // el de menor carga actual
    }

    [Fact]
    public async Task SelectAvailableVehicleAsync_NingunoConCapacidad_LanzaNoAvailableVehicleException()
    {
        var vehicle = new Vehicle("DEF456", 300, 6);
        SetVehicleId(vehicle, 1);

        _vehicleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Vehicle> { vehicle });
        _shipmentRepoMock.Setup(r => r.GetActiveLoadForVehicleAsync(1)).ReturnsAsync((290m, 5.5m));

        var sut = CreateSut();
        var act = async () => await sut.SelectAvailableVehicleAsync(requiredWeightKg: 50, requiredVolumeM3: 1);

        await act.Should().ThrowAsync<NoAvailableVehicleException>();
    }

    /// <summary>
    /// Helper para setear el Id (private set) de Vehicle vía reflexión, ya que
    /// el constructor de dominio no acepta Id (lo genera EF Core al persistir).
    /// Es una concesión pragmática solo para tests; en producción el Id siempre
    /// viene de la BD.
    /// </summary>
    private static void SetVehicleId(Vehicle vehicle, int id)
    {
        var property = typeof(Vehicle).GetProperty(nameof(Vehicle.Id),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        property.SetValue(vehicle, id);
    }
}
