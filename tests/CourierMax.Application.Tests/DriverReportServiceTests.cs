using CourierMax.Application.Services;
using CourierMax.Domain.Entities;
using CourierMax.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CourierMax.Application.Tests;

public sealed class DriverReportServiceTests
{
    private readonly Mock<IDriverRepository> _driverRepoMock = new();
    private readonly Mock<IShipmentRepository> _shipmentRepoMock = new();

    private DriverReportService CreateSut() => new(_driverRepoMock.Object, _shipmentRepoMock.Object);

    [Fact]
    public async Task GenerateReportAsync_ConEntregasDentroYFueraDeSla_CalculaPorcentajeCorrecto()
    {
        var driver = new Driver("Juan Pérez", vehicleId: 1);
        _driverRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(driver);

        // 2 entregas: una dentro de SLA, una fuera. Se espera 50% de cumplimiento.
        var projections = new List<DriverDeliveryProjection>
        {
            new(AssignedAt: new DateTime(2026, 6, 1), CreatedAt: new DateTime(2026, 6, 1),
                DeliveredAt: new DateTime(2026, 6, 3), SlaDeadline: new DateOnly(2026, 6, 5)), // dentro
            new(AssignedAt: new DateTime(2026, 6, 1), CreatedAt: new DateTime(2026, 6, 1),
                DeliveredAt: new DateTime(2026, 6, 10), SlaDeadline: new DateOnly(2026, 6, 5)) // fuera
        };

        var aggregates = new DriverShipmentAggregates(
            TotalAssigned: 5, TotalDelivered: 2, TotalCancelled: 1, TotalInTransit: 2,
            TotalWeightKg: 25m, DeliveryProjections: projections);

        _shipmentRepoMock.Setup(r => r.GetDriverAggregatesAsync(1)).ReturnsAsync(aggregates);

        var sut = CreateSut();
        var report = await sut.GenerateReportAsync(1);

        report.SlaCompliancePercentage.Should().Be(50);
        report.TotalAssigned.Should().Be(5);
        report.TotalDelivered.Should().Be(2);
        report.TotalCancelled.Should().Be(1);
        report.TotalInTransit.Should().Be(2);
        report.TotalWeightKgTransported.Should().Be(25m);
    }

    [Fact]
    public async Task GenerateReportAsync_SinEntregas_RetornaCerosSinLanzarExcepcion()
    {
        var driver = new Driver("María López", vehicleId: 2);
        _driverRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(driver);

        var aggregates = new DriverShipmentAggregates(
            TotalAssigned: 3, TotalDelivered: 0, TotalCancelled: 1, TotalInTransit: 2,
            TotalWeightKg: 10m, DeliveryProjections: new List<DriverDeliveryProjection>());

        _shipmentRepoMock.Setup(r => r.GetDriverAggregatesAsync(2)).ReturnsAsync(aggregates);

        var sut = CreateSut();
        var report = await sut.GenerateReportAsync(2);

        report.AverageDeliveryDays.Should().Be(0);
        report.SlaCompliancePercentage.Should().Be(0);
    }

    [Fact]
    public async Task GenerateReportAsync_ConductorInexistente_LanzaInvalidOperationException()
    {
        _driverRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Driver?)null);

        var sut = CreateSut();
        var act = async () => await sut.GenerateReportAsync(99);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
