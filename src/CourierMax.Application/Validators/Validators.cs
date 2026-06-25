using CourierMax.Application.DTOs;
using CourierMax.Domain.Enums;
using FluentValidation;

namespace CourierMax.Application.Validators;

/// <summary>
/// RN-04: validaciones de datos de entrada. El formato de teléfono colombiano
/// (10 dígitos, inicia con 3 o 6) y los rangos de peso/dimensiones se validan
/// aquí; las reglas que dependen de estado del sistema (ciudad existe, ruta
/// tarifada existe) se resuelven en el Service porque requieren acceso a datos,
/// algo que un validador de FluentValidation puro debería evitar mezclar.
/// </summary>
public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    private const string PhonePattern = @"^[36]\d{9}$";

    public CreateShipmentRequestValidator()
    {
        // MaximumLength es defensa en profundidad: además de la validación,
        // evita que un payload con strings desproporcionados (ej. 1MB en
        // SenderAddress) llegue a EF Core y falle recién al guardar con un
        // error 500 confuso. Los límites coinciden con HasMaxLength en
        // CourierMaxDbContext (150 para nombres, 300 para direcciones).
        RuleFor(x => x.SenderName).NotEmpty().MaximumLength(150)
            .WithMessage("El nombre del remitente es obligatorio y debe tener máximo 150 caracteres.");
        RuleFor(x => x.SenderPhone).Matches(PhonePattern)
            .WithMessage("El teléfono del remitente debe tener 10 dígitos e iniciar con 3 o 6.");
        RuleFor(x => x.SenderAddress).NotEmpty().MaximumLength(300)
            .WithMessage("La dirección de recogida es obligatoria y debe tener máximo 300 caracteres.");

        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(150)
            .WithMessage("El nombre del destinatario es obligatorio y debe tener máximo 150 caracteres.");
        RuleFor(x => x.RecipientPhone).Matches(PhonePattern)
            .WithMessage("El teléfono del destinatario debe tener 10 dígitos e iniciar con 3 o 6.");
        RuleFor(x => x.RecipientAddress).NotEmpty().MaximumLength(300)
            .WithMessage("La dirección de entrega es obligatoria y debe tener máximo 300 caracteres.");

        RuleFor(x => x.PackageWeightKg).InclusiveBetween(0.1m, 100m)
            .WithMessage("El peso debe estar entre 0.1 y 100 kg.");
        RuleFor(x => x.PackageLengthCm).InclusiveBetween(1m, 200m)
            .WithMessage("El largo debe estar entre 1 y 200 cm.");
        RuleFor(x => x.PackageWidthCm).InclusiveBetween(1m, 200m)
            .WithMessage("El ancho debe estar entre 1 y 200 cm.");
        RuleFor(x => x.PackageHeightCm).InclusiveBetween(1m, 200m)
            .WithMessage("El alto debe estar entre 1 y 200 cm.");

        RuleFor(x => x.PackageType).IsInEnum();
        RuleFor(x => x.ServiceType).IsInEnum();

        RuleFor(x => x.OriginCity).NotEmpty().MaximumLength(100)
            .WithMessage("La ciudad de origen es obligatoria.");
        RuleFor(x => x.DestinationCity).NotEmpty().MaximumLength(100)
            .WithMessage("La ciudad de destino es obligatoria.");
        RuleFor(x => x)
            .Must(x => !string.Equals(x.OriginCity, x.DestinationCity, StringComparison.OrdinalIgnoreCase))
            .WithMessage("La ciudad de origen y destino no pueden ser la misma.");
    }
}

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum();
        RuleFor(x => x.ChangedByUserId).NotEmpty().MaximumLength(100)
            .WithMessage("ChangedByUserId es obligatorio y debe tener máximo 100 caracteres.");

        // RN-03: motivo obligatorio (mínimo 5 caracteres) solo cuando se cancela.
        RuleFor(x => x.Reason)
            .NotEmpty().MinimumLength(5).MaximumLength(500)
            .When(x => x.NewStatus == ShipmentStatus.Cancelado)
            .WithMessage("El motivo de cancelación es obligatorio y debe tener entre 5 y 500 caracteres.");
    }
}

public sealed class AssignShipmentRequestValidator : AbstractValidator<AssignShipmentRequest>
{
    public AssignShipmentRequestValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0).WithMessage("DriverId es obligatorio.");
    }
}
