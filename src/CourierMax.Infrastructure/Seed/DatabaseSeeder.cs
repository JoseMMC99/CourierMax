using CourierMax.Domain.Entities;
using CourierMax.Infrastructure.Persistence;

namespace CourierMax.Infrastructure.Seed;

/// <summary>
/// Carga los "Datos de Referencia" del enunciado: ciudades, distancias/tarifas,
/// festivos colombianos 2026, y los 3 vehículos/conductores de ejemplo.
/// Se ejecuta una sola vez al iniciar la app si la BD está vacía (ver Program.cs).
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(CourierMaxDbContext context)
    {
        if (context.Cities.Any())
            return; // ya seedeada

        var bogota = new City("Bogotá");
        var medellin = new City("Medellín");
        var cali = new City("Cali");
        var barranquilla = new City("Barranquilla");

        context.Cities.AddRange(bogota, medellin, cali, barranquilla);
        context.SaveChanges();

        context.CityDistances.AddRange(
            new CityDistance(bogota.Id, medellin.Id, 480, 12_000m),
            new CityDistance(bogota.Id, cali.Id, 360, 9_000m),
            new CityDistance(bogota.Id, barranquilla.Id, 950, 20_000m),
            new CityDistance(medellin.Id, cali.Id, 310, 8_000m),
            new CityDistance(medellin.Id, barranquilla.Id, 650, 15_000m),
            new CityDistance(cali.Id, barranquilla.Id, 900, 18_000m));

        // Festivos colombianos 2026, tal como los lista el enunciado.
        int[][] holidayDates =
        {
            new[] { 1, 1 }, new[] { 1, 26 }, new[] { 1, 30 }, new[] { 3, 24 },
            new[] { 5, 1 }, new[] { 6, 1 }, new[] { 6, 29 }, new[] { 7, 20 },
            new[] { 8, 17 }, new[] { 10, 20 }, new[] { 11, 9 }, new[] { 12, 8 }
        };
        foreach (var hd in holidayDates)
            context.PublicHolidays.Add(new PublicHoliday(new DateOnly(2026, hd[0], hd[1])));

        var vehicle1 = new Vehicle("ABC123", 500, 10);
        var vehicle2 = new Vehicle("DEF456", 300, 6);
        var vehicle3 = new Vehicle("GHI789", 800, 15);
        context.Vehicles.AddRange(vehicle1, vehicle2, vehicle3);
        context.SaveChanges();

        context.Drivers.AddRange(
            new Driver("Juan Pérez", vehicle1.Id),
            new Driver("María López", vehicle2.Id),
            new Driver("Carlos Ruiz", vehicle3.Id));

        context.SaveChanges();
    }
}
