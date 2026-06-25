using CourierMax.Application.DTOs;
using CourierMax.Application.Validators;
using CourierMax.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CourierMax.Application.Tests;

public sealed class CreateShipmentRequestValidatorTests
{
    private readonly CreateShipmentRequestValidator _sut = new();

    private static CreateShipmentRequest ValidRequest() => new(
        SenderName: "Juan Pérez", SenderPhone: "3001234567", SenderAddress: "Calle 1 #2-3",
        RecipientName: "Ana Gómez", RecipientPhone: "6012345678", RecipientAddress: "Av Siempre Viva 742",
        PackageWeightKg: 5m, PackageLengthCm: 30, PackageWidthCm: 20, PackageHeightCm: 15,
        PackageType: PackageType.Fragil, ServiceType: ServiceType.Express,
        OriginCity: "Bogotá", DestinationCity: "Medellín");

    [Fact]
    public void Validate_RequestValido_NoTieneErrores()
    {
        var result = _sut.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("123456789")]   // 9 dígitos
    [InlineData("12345678901")] // 11 dígitos
    [InlineData("5001234567")]  // no inicia con 3 o 6
    public void Validate_TelefonoInvalido_RetornaError(string invalidPhone)
    {
        var request = ValidRequest() with { SenderPhone = invalidPhone };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.05)]  // menor al mínimo
    [InlineData(100.1)] // mayor al máximo
    public void Validate_PesoFueraDeRango_RetornaError(decimal invalidWeight)
    {
        var request = ValidRequest() with { PackageWeightKg = invalidWeight };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MismaCiudadOrigenYDestino_RetornaError()
    {
        var request = ValidRequest() with { OriginCity = "Bogotá", DestinationCity = "Bogotá" };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }
}

public sealed class ChangeStatusRequestValidatorTests
{
    private readonly ChangeStatusRequestValidator _sut = new();

    [Fact]
    public void Validate_CancelacionSinMotivo_RetornaError()
    {
        var request = new ChangeStatusRequest(ShipmentStatus.Cancelado, "user1", Reason: null);

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_CancelacionConMotivoValido_NoTieneErrores()
    {
        var request = new ChangeStatusRequest(ShipmentStatus.Cancelado, "user1", "Cliente canceló el pedido");

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TransicionNoCancelacionSinMotivo_NoTieneErrores()
    {
        // El motivo solo es obligatorio para Cancelado; otras transiciones no lo requieren.
        var request = new ChangeStatusRequest(ShipmentStatus.EnTransito, "user1", Reason: null);

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
