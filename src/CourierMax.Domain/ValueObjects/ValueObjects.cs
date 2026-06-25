namespace CourierMax.Domain.ValueObjects;

/// <summary>
/// VO inmutable para datos de remitente o destinatario.
/// Se valida en el constructor porque un ContactInfo nunca debe existir en estado inválido
/// (la validación de formato detallada vive en FluentValidation a nivel de Application,
/// esto es solo una guarda de invariantes mínimas del dominio).
/// </summary>
public sealed class ContactInfo
{
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;

    private ContactInfo() { } // EF Core (Owned Type)

    public ContactInfo(string name, string phone, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(name));
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("El teléfono no puede estar vacío.", nameof(phone));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("La dirección no puede estar vacía.", nameof(address));

        Name = name;
        Phone = phone;
        Address = address;
    }
}

public sealed class PackageInfo
{
    public decimal WeightKg { get; private set; }
    public decimal LengthCm { get; private set; }
    public decimal WidthCm { get; private set; }
    public decimal HeightCm { get; private set; }
    public Enums.PackageType Type { get; private set; }

    private PackageInfo() { } // EF Core (Owned Type)

    public PackageInfo(decimal weightKg, decimal lengthCm, decimal widthCm, decimal heightCm, Enums.PackageType type)
    {
        if (weightKg < 0.1m || weightKg > 100m)
            throw new ArgumentOutOfRangeException(nameof(weightKg), "El peso debe estar entre 0.1 y 100 kg.");
        if (lengthCm < 1 || lengthCm > 200 || widthCm < 1 || widthCm > 200 || heightCm < 1 || heightCm > 200)
            throw new ArgumentOutOfRangeException(nameof(lengthCm), "Cada dimensión debe estar entre 1 y 200 cm.");

        WeightKg = weightKg;
        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
        Type = type;
    }

    public decimal VolumeM3 => (LengthCm * WidthCm * HeightCm) / 1_000_000m;
}

/// <summary>
/// Código de rastreo con formato CM-XXXXXXXX (8 dígitos).
/// La unicidad NO se valida aquí (responsabilidad de infraestructura/repositorio);
/// este VO solo garantiza el formato.
/// </summary>
public sealed class TrackingCode
{
    private const string Prefix = "CM-";
    public string Value { get; }

    public TrackingCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsValidFormat(value))
            throw new ArgumentException(
                $"El código de rastreo debe tener el formato {Prefix}XXXXXXXX (8 dígitos). Recibido: '{value}'.",
                nameof(value));

        Value = value;
    }

    public static bool IsValidFormat(string value)
    {
        if (!value.StartsWith(Prefix)) return false;
        var digits = value[Prefix.Length..];
        return digits.Length == 8 && digits.All(char.IsDigit);
    }

    public override string ToString() => Value;
}
