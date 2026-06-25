namespace CourierMax.Domain.Entities;

public sealed class City
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private City() { } // EF Core

    public City(string name)
    {
        Name = name;
    }
}
