using CourierMax.Application.DTOs;
using CourierMax.Application.Interfaces;
using CourierMax.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourierMax.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly IDriverReportService _driverReportService;

    public DriversController(IDriverReportService driverReportService)
    {
        _driverReportService = driverReportService;
    }

    /// <summary>RF-06: reporte de métricas de eficiencia por conductor.</summary>
    [HttpGet("{id:int}/report")]
    [ProducesResponseType(typeof(DriverReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReport(int id)
    {
        var result = await _driverReportService.GenerateReportAsync(id);
        return Ok(result);
    }
}

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleQueryService _vehicleQueryService;

    public VehiclesController(IVehicleQueryService vehicleQueryService)
    {
        _vehicleQueryService = vehicleQueryService;
    }

    /// <summary>Lista vehículos de la flota junto con su carga actual (RN-01).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var result = await _vehicleQueryService.ListWithLoadAsync();
        return Ok(result);
    }
}

[ApiController]
[Route("api/cities")]
public sealed class CitiesController : ControllerBase
{
    private readonly ICityRepository _cityRepository;

    public CitiesController(ICityRepository cityRepository)
    {
        _cityRepository = cityRepository;
    }

    /// <summary>Catálogo de ciudades válidas en el sistema (soporte para RN-04 / Swagger).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var cities = await _cityRepository.GetAllAsync();
        return Ok(cities.Select(c => new CityResponse(c.Id, c.Name)));
    }
}
