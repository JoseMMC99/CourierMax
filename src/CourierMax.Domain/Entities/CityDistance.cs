namespace CourierMax.Domain.Entities;

/// <summary>
/// Tarifa de distancia entre un par de ciudades. Se asume simétrica
/// (Bogotá→Cali cuesta lo mismo que Cali→Bogotá), tal como se presenta
/// la tabla de referencia en el enunciado.
/// </summary>
public sealed class CityDistance
{
    public int Id { get; private set; }
    public int OriginCityId { get; private set; }
    public int DestinationCityId { get; private set; }
    public decimal DistanceKm { get; private set; }
    public decimal DistanceFee { get; private set; }

    private CityDistance() { } // EF Core

    public CityDistance(int originCityId, int destinationCityId, decimal distanceKm, decimal distanceFee)
    {
        OriginCityId = originCityId;
        DestinationCityId = destinationCityId;
        DistanceKm = distanceKm;
        DistanceFee = distanceFee;
    }
}
