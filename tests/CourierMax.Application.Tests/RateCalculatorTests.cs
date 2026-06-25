using CourierMax.Application.Services;
using CourierMax.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CourierMax.Application.Tests;

public sealed class RateCalculatorTests
{
    private readonly RateCalculator _sut = new();

    [Fact]
    public void Calculate_EjemploExactoDelEnunciado_RetornaCostoEsperado()
    {
        // Paquete frágil de 5kg, servicio express, distancia Bogotá-Medellín ($12,000).
        // Esperado según el enunciado: $40,950.
        var result = _sut.Calculate(ServiceType.Express, weightKg: 5m, PackageType.Fragil, distanceFee: 12_000m);

        result.Should().Be(40_950m);
    }

    [Theory]
    [InlineData(ServiceType.Estandar, 8_000)]
    [InlineData(ServiceType.Express, 15_000)]
    [InlineData(ServiceType.MismoDia, 25_000)]
    public void Calculate_SinRecargos_RetornaSoloTarifaBase(ServiceType serviceType, decimal expectedBase)
    {
        // Peso = 2kg (sin recargo), documento (sin recargo), distancia = 0.
        var result = _sut.Calculate(serviceType, weightKg: 2m, PackageType.Documento, distanceFee: 0m);

        result.Should().Be(expectedBase);
    }

    [Fact]
    public void Calculate_PesoMenorAUmbral_NoAplicaRecargoDePeso()
    {
        var result = _sut.Calculate(ServiceType.Estandar, weightKg: 1.5m, PackageType.Documento, distanceFee: 0m);

        result.Should().Be(8_000m); // sin recargo, ya que 1.5kg < 2kg
    }

    [Fact]
    public void Calculate_RecargoPerecedero_Aplica25PorCiento()
    {
        // Base estándar (8000) + sin peso extra + sin distancia = 8000; ×25% = 2000.
        var result = _sut.Calculate(ServiceType.Estandar, weightKg: 2m, PackageType.Perecedero, distanceFee: 0m);

        result.Should().Be(10_000m);
    }

    [Fact]
    public void Calculate_PaqueteNormal_NoAplicaRecargoPorTipo()
    {
        var result = _sut.Calculate(ServiceType.Estandar, weightKg: 2m, PackageType.Paquete, distanceFee: 0m);

        result.Should().Be(8_000m);
    }
}
