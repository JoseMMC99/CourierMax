using CourierMax.Domain.Entities;
using CourierMax.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CourierMax.Domain.Tests;

public sealed class VehicleTests
{
    [Fact]
    public void EnsureCapacity_DentroDelLimite_NoLanzaExcepcion()
    {
        var vehicle = new Vehicle("ABC123", maxWeightKg: 500, maxVolumeM3: 10);

        var act = () => vehicle.EnsureCapacity(currentWeightKg: 100, currentVolumeM3: 2,
            additionalWeightKg: 50, additionalVolumeM3: 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCapacity_ExcedePesoMaximo_LanzaVehicleCapacityExceededException()
    {
        var vehicle = new Vehicle("ABC123", maxWeightKg: 500, maxVolumeM3: 10);

        var act = () => vehicle.EnsureCapacity(currentWeightKg: 480, currentVolumeM3: 2,
            additionalWeightKg: 50, additionalVolumeM3: 1);

        act.Should().Throw<VehicleCapacityExceededException>();
    }

    [Fact]
    public void EnsureCapacity_ExcedeVolumenMaximo_LanzaVehicleCapacityExceededException()
    {
        var vehicle = new Vehicle("ABC123", maxWeightKg: 500, maxVolumeM3: 10);

        var act = () => vehicle.EnsureCapacity(currentWeightKg: 100, currentVolumeM3: 9.5m,
            additionalWeightKg: 10, additionalVolumeM3: 1);

        act.Should().Throw<VehicleCapacityExceededException>();
    }

    [Fact]
    public void EnsureCapacity_ExactamenteEnElLimite_NoLanzaExcepcion()
    {
        var vehicle = new Vehicle("ABC123", maxWeightKg: 500, maxVolumeM3: 10);

        var act = () => vehicle.EnsureCapacity(currentWeightKg: 450, currentVolumeM3: 9,
            additionalWeightKg: 50, additionalVolumeM3: 1);

        act.Should().NotThrow(); // 500/500 y 10/10 exactos, no excede
    }
}
