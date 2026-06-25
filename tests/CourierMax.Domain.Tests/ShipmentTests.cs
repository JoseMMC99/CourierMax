using CourierMax.Domain.Entities;
using CourierMax.Domain.Enums;
using CourierMax.Domain.Exceptions;
using CourierMax.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CourierMax.Domain.Tests;

public sealed class ShipmentTests
{
    private static Shipment CreateSampleShipment(DateTime? createdAt = null, DateOnly? slaDeadline = null)
    {
        var sender = new ContactInfo("Juan Pérez", "3001234567", "Calle 1 #2-3");
        var recipient = new ContactInfo("Ana Gómez", "3109876543", "Av Siempre Viva 742");
        var package = new PackageInfo(5m, 30m, 20m, 15m, PackageType.Fragil);
        var created = createdAt ?? new DateTime(2026, 6, 24);

        return Shipment.Create(
            "CM-00000001", sender, recipient, package, ServiceType.Express,
            originCityId: 1, destinationCityId: 2, cost: 40_950m,
            slaDeadline: slaDeadline ?? DateOnly.FromDateTime(created).AddDays(2),
            createdAt: created);
    }

    [Fact]
    public void Create_NuevoEnvio_EstadoInicialEsCreadoYTieneCodigoUnico()
    {
        var shipment = CreateSampleShipment();

        shipment.Status.Should().Be(ShipmentStatus.Creado);
        shipment.TrackingCode.Should().Be("CM-00000001");
        shipment.StatusHistory.Should().ContainSingle(sc => sc.ToStatus == ShipmentStatus.Creado);
    }

    [Fact]
    public void AssignTo_DesdeCreado_TransicionaAAsignado()
    {
        var shipment = CreateSampleShipment();

        shipment.AssignTo(driverId: 1, vehicleId: 1, "user1", DateTime.UtcNow);

        shipment.Status.Should().Be(ShipmentStatus.Asignado);
        shipment.AssignedDriverId.Should().Be(1);
        shipment.AssignedVehicleId.Should().Be(1);
    }

    [Fact]
    public void MarkDelivered_DirectamenteDesdeCreado_LanzaInvalidStatusTransitionException()
    {
        var shipment = CreateSampleShipment();

        var act = () => shipment.MarkDelivered("user1", DateTime.UtcNow);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void FlujoCompleto_CreadoAsignadoEnTransitoEntregado_TransicionaCorrectamente()
    {
        var shipment = CreateSampleShipment();

        shipment.AssignTo(1, 1, "user1", DateTime.UtcNow);
        shipment.MarkInTransit("user1", DateTime.UtcNow);
        shipment.MarkDelivered("user1", DateTime.UtcNow);

        shipment.Status.Should().Be(ShipmentStatus.Entregado);
        shipment.StatusHistory.Should().HaveCount(4); // Creado, Asignado, EnTransito, Entregado
    }

    [Fact]
    public void Cancel_DesdeAsignado_TransicionaACanceladoConMotivo()
    {
        var shipment = CreateSampleShipment();
        shipment.AssignTo(1, 1, "user1", DateTime.UtcNow);

        shipment.Cancel("Cliente cambió de opinión", "user1", DateTime.UtcNow);

        shipment.Status.Should().Be(ShipmentStatus.Cancelado);
        shipment.StatusHistory.Last().Reason.Should().Be("Cliente cambió de opinión");
    }

    [Fact]
    public void Cancel_DesdeEntregado_LanzaInvalidStatusTransitionException()
    {
        var shipment = CreateSampleShipment();
        shipment.AssignTo(1, 1, "user1", DateTime.UtcNow);
        shipment.MarkInTransit("user1", DateTime.UtcNow);
        shipment.MarkDelivered("user1", DateTime.UtcNow);

        var act = () => shipment.Cancel("Motivo válido", "user1", DateTime.UtcNow);

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("   ")]
    public void Cancel_ConMotivoMenorA5Caracteres_LanzaCancellationReasonRequiredException(string reason)
    {
        var shipment = CreateSampleShipment();

        var act = () => shipment.Cancel(reason, "user1", DateTime.UtcNow);

        act.Should().Throw<CancellationReasonRequiredException>();
    }

    [Fact]
    public void IsDelayed_FechaPosteriorASlaSinEntregar_RetornaTrue()
    {
        var shipment = CreateSampleShipment(
            createdAt: new DateTime(2026, 6, 1),
            slaDeadline: new DateOnly(2026, 6, 5));

        shipment.IsDelayed(new DateOnly(2026, 6, 10)).Should().BeTrue();
    }

    [Fact]
    public void IsDelayed_EnvioYaEntregado_RetornaFalseAunquePaseElSla()
    {
        var shipment = CreateSampleShipment(
            createdAt: new DateTime(2026, 6, 1),
            slaDeadline: new DateOnly(2026, 6, 5));
        shipment.AssignTo(1, 1, "user1", DateTime.UtcNow);
        shipment.MarkInTransit("user1", DateTime.UtcNow);
        shipment.MarkDelivered("user1", DateTime.UtcNow);

        shipment.IsDelayed(new DateOnly(2026, 6, 10)).Should().BeFalse();
    }
}
