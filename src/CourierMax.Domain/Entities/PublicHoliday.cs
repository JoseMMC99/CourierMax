namespace CourierMax.Domain.Entities;

public sealed class PublicHoliday
{
    public int Id { get; private set; }
    public DateOnly Date { get; private set; }

    private PublicHoliday() { } // EF Core

    public PublicHoliday(DateOnly date)
    {
        Date = date;
    }
}
