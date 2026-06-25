using CourierMax.Application.Interfaces;
using CourierMax.Domain.Enums;
using CourierMax.Domain.Interfaces;

namespace CourierMax.Application.Services;

/// <summary>
/// Calendario de días hábiles colombiano (RN-02). Los festivos se obtienen
/// del repositorio (tabla PublicHolidays, seedeada con los festivos 2026 del
/// enunciado) en lugar de hardcodearse aquí, para que el calendario sea
/// extensible a otros años sin tocar código.
/// </summary>
public sealed class BusinessDayCalculator : IBusinessDayCalculator
{
    private readonly IPublicHolidayRepository _holidayRepository;

    public BusinessDayCalculator(IPublicHolidayRepository holidayRepository)
    {
        _holidayRepository = holidayRepository;
    }

    public DateOnly CalculateSlaDeadline(DateTime createdAt, ServiceType serviceType)
    {
        var slaBusinessDays = GetSlaBusinessDays(serviceType);
        var createdDate = DateOnly.FromDateTime(createdAt);

        // Mismo día: el SLA es el propio día de creación, sin avanzar (RF-05: 0 días hábiles).
        if (slaBusinessDays == 0)
            return createdDate;

        var holidays = _holidayRepository.GetAllAsync().GetAwaiter().GetResult();
        return AddBusinessDays(createdDate, slaBusinessDays, holidays);
    }

    public bool IsBusinessDay(DateOnly date)
    {
        var holidays = _holidayRepository.GetAllAsync().GetAwaiter().GetResult();
        return IsBusinessDayInternal(date, holidays);
    }

    private static int GetSlaBusinessDays(ServiceType serviceType) => serviceType switch
    {
        ServiceType.Estandar => 5,
        ServiceType.Express => 2,
        ServiceType.MismoDia => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(serviceType))
    };

    /// <summary>
    /// Avanza N días hábiles desde una fecha, saltando sábados, domingos y festivos.
    /// Ejemplo del enunciado: viernes + 1 día hábil → lunes (no cuenta sáb/dom).
    /// </summary>
    private static DateOnly AddBusinessDays(DateOnly start, int businessDays, HashSet<DateOnly> holidays)
    {
        var current = start;
        var remaining = businessDays;

        while (remaining > 0)
        {
            current = current.AddDays(1);
            if (IsBusinessDayInternal(current, holidays))
                remaining--;
        }

        return current;
    }

    private static bool IsBusinessDayInternal(DateOnly date, HashSet<DateOnly> holidays)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return !isWeekend && !holidays.Contains(date);
    }
}
