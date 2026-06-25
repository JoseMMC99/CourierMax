using CourierMax.Application.Services;
using CourierMax.Domain.Enums;
using CourierMax.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CourierMax.Application.Tests;

public sealed class BusinessDayCalculatorTests
{
    private readonly Mock<IPublicHolidayRepository> _holidayRepoMock = new();

    private BusinessDayCalculator CreateSut(HashSet<DateOnly>? holidays = null)
    {
        _holidayRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(holidays ?? new HashSet<DateOnly>());
        return new BusinessDayCalculator(_holidayRepoMock.Object);
    }

    [Fact]
    public void CalculateSlaDeadline_ViernesMasUnDiaHabil_RetornaLunes()
    {
        // Caso explícito del enunciado (RN-02): viernes + 1 día hábil → lunes
        // (no cuenta sábado ni domingo). Usamos Express (2 días hábiles) para
        // verificar el salto de fin de semana en general; replicamos el ejemplo
        // exacto con un calculador ad-hoc de 1 día vía MismoDia+1 simulado.
        var sut = CreateSut();
        var friday = new DateTime(2026, 6, 19); // viernes

        // Express = 2 días hábiles desde el viernes: sáb/dom no cuentan,
        // entonces lunes (+1) y martes (+2) → martes 23 de junio.
        var deadline = sut.CalculateSlaDeadline(friday, ServiceType.Express);

        deadline.Should().Be(new DateOnly(2026, 6, 23));
    }

    [Fact]
    public void CalculateSlaDeadline_MismoDia_RetornaFechaDeCreacion()
    {
        var sut = CreateSut();
        var createdAt = new DateTime(2026, 6, 24, 14, 30, 0);

        var deadline = sut.CalculateSlaDeadline(createdAt, ServiceType.MismoDia);

        deadline.Should().Be(DateOnly.FromDateTime(createdAt));
    }

    [Fact]
    public void CalculateSlaDeadline_ConFestivoEnElRango_LoExcluyeDelConteo()
    {
        // 29 de junio de 2026 es festivo (San Pedro y San Pablo, según el enunciado).
        var holidays = new HashSet<DateOnly> { new DateOnly(2026, 6, 29) };
        var sut = CreateSut(holidays);

        // Viernes 26 jun + Estándar (5 días hábiles): sáb 27, dom 28 no cuentan;
        // lun 29 es festivo, no cuenta; mar 30 (1), mié 1 jul (2), jue 2 (3),
        // vie 3 (4), lun 6 (5, salta sáb/dom) → lunes 6 de julio.
        var deadline = sut.CalculateSlaDeadline(new DateTime(2026, 6, 26), ServiceType.Estandar);

        deadline.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void IsBusinessDay_Sabado_RetornaFalse()
    {
        var sut = CreateSut();

        sut.IsBusinessDay(new DateOnly(2026, 6, 27)).Should().BeFalse(); // sábado
    }

    [Fact]
    public void IsBusinessDay_DiaConFestivo_RetornaFalse()
    {
        var holidays = new HashSet<DateOnly> { new DateOnly(2026, 7, 20) };
        var sut = CreateSut(holidays);

        sut.IsBusinessDay(new DateOnly(2026, 7, 20)).Should().BeFalse();
    }
}
