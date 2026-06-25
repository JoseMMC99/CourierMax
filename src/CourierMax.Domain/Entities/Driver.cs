namespace CourierMax.Domain.Entities;

public sealed class Driver
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    private Driver() { } // EF Core

    public Driver(string name, int? vehicleId = null, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del conductor no puede estar vacío.", nameof(name));

        Name = name;
        VehicleId = vehicleId;
        IsActive = isActive;
    }

    public void EnsureActive()
    {
        if (!IsActive)
            throw new Exceptions.InactiveDriverException(Id);
    }
}
