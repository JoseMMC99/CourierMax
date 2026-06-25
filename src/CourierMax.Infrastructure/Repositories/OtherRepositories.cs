using CourierMax.Domain.Entities;
using CourierMax.Domain.Interfaces;
using CourierMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourierMax.Infrastructure.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly CourierMaxDbContext _context;
    public VehicleRepository(CourierMaxDbContext context) => _context = context;

    public Task<Vehicle?> GetByIdAsync(int id) => _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
    public Task<List<Vehicle>> GetAllAsync() => _context.Vehicles.ToListAsync();
}

public sealed class DriverRepository : IDriverRepository
{
    private readonly CourierMaxDbContext _context;
    public DriverRepository(CourierMaxDbContext context) => _context = context;

    public Task<Driver?> GetByIdAsync(int id) => _context.Drivers.FirstOrDefaultAsync(d => d.Id == id);
    public Task<List<Driver>> GetAllAsync() => _context.Drivers.ToListAsync();
}

public sealed class CityRepository : ICityRepository
{
    private readonly CourierMaxDbContext _context;
    public CityRepository(CourierMaxDbContext context) => _context = context;

    public Task<City?> GetByIdAsync(int id) => _context.Cities.FirstOrDefaultAsync(c => c.Id == id);

    public Task<City?> GetByNameAsync(string name) =>
        _context.Cities.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

    public Task<List<City>> GetAllAsync() => _context.Cities.OrderBy(c => c.Name).ToListAsync();

    public Task<CityDistance?> GetDistanceAsync(int originCityId, int destinationCityId)
    {
        // La tarifa de distancia se asume simétrica (ver CityDistance.cs),
        // así que se busca en ambos sentidos.
        return _context.CityDistances.FirstOrDefaultAsync(cd =>
            (cd.OriginCityId == originCityId && cd.DestinationCityId == destinationCityId) ||
            (cd.OriginCityId == destinationCityId && cd.DestinationCityId == originCityId));
    }
}

public sealed class PublicHolidayRepository : IPublicHolidayRepository
{
    private readonly CourierMaxDbContext _context;
    public PublicHolidayRepository(CourierMaxDbContext context) => _context = context;

    public async Task<HashSet<DateOnly>> GetAllAsync()
    {
        var dates = await _context.PublicHolidays.Select(h => h.Date).ToListAsync();
        return dates.ToHashSet();
    }
}
