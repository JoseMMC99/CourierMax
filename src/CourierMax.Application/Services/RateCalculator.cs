using CourierMax.Application.Interfaces;
using CourierMax.Domain.Enums;

namespace CourierMax.Application.Services;

/// <summary>
/// Implementa RF-04 exactamente como lo describe el enunciado:
///   1. Tarifa base por ServiceType.
///   2. + Recargo por peso: $1,500 por kg que exceda los primeros 2kg.
///   3. + Tarifa de distancia (pasada como parámetro, resuelta por el caller vía CityDistance).
///   4. × Recargo por tipo de paquete (Frágil +30%, Perecedero +25%, resto sin recargo),
///      aplicado sobre la SUMA de los tres anteriores (ver ejemplo del enunciado).
/// </summary>
public sealed class RateCalculator : IRateCalculator
{
    private const decimal WeightSurchargePerKg = 1500m;
    private const decimal FreeWeightThresholdKg = 2m;

    public decimal Calculate(ServiceType serviceType, decimal weightKg, PackageType packageType, decimal distanceFee)
    {
        var baseRate = GetBaseRate(serviceType);
        var weightSurcharge = GetWeightSurcharge(weightKg);
        var subtotal = baseRate + weightSurcharge + distanceFee;

        var packageMultiplier = GetPackageSurchargeMultiplier(packageType);
        var packageSurcharge = subtotal * packageMultiplier;

        return subtotal + packageSurcharge;
    }

    private static decimal GetBaseRate(ServiceType serviceType) => serviceType switch
    {
        ServiceType.Estandar => 8_000m,
        ServiceType.Express => 15_000m,
        ServiceType.MismoDia => 25_000m,
        _ => throw new ArgumentOutOfRangeException(nameof(serviceType))
    };

    private static decimal GetWeightSurcharge(decimal weightKg)
    {
        var extraKg = Math.Max(0, weightKg - FreeWeightThresholdKg);
        return extraKg * WeightSurchargePerKg;
    }

    private static decimal GetPackageSurchargeMultiplier(PackageType packageType) => packageType switch
    {
        PackageType.Fragil => 0.30m,
        PackageType.Perecedero => 0.25m,
        PackageType.Documento => 0m,
        PackageType.Paquete => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(packageType))
    };
}
