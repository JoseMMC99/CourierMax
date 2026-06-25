using CourierMax.Application.Interfaces;
using CourierMax.Domain.Interfaces;

namespace CourierMax.Application.Services;

/// <summary>
/// Genera códigos de rastreo únicos con formato CM-XXXXXXXX (RF-01, RN-05).
/// Reintenta en el improbable caso de colisión (8 dígitos = 100M combinaciones,
/// pero se valida unicidad real contra la BD en vez de asumirla).
/// </summary>
public sealed class TrackingCodeGenerator : ITrackingCodeGenerator
{
    private const int MaxAttempts = 10;
    private static readonly Random Random = new();

    private readonly IShipmentRepository _shipmentRepository;

    public TrackingCodeGenerator(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<string> GenerateUniqueAsync()
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = $"CM-{Random.Next(0, 100_000_000):D8}";
            if (!await _shipmentRepository.TrackingCodeExistsAsync(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"No se pudo generar un código de rastreo único tras {MaxAttempts} intentos.");
    }
}
