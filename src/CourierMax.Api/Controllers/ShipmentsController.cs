using CourierMax.Application.DTOs;
using CourierMax.Application.Interfaces;
using CourierMax.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CourierMax.Api.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _shipmentService;
    private readonly IValidator<CreateShipmentRequest> _createValidator;
    private readonly IValidator<ChangeStatusRequest> _changeStatusValidator;
    private readonly IValidator<AssignShipmentRequest> _assignValidator;

    public ShipmentsController(
        IShipmentService shipmentService,
        IValidator<CreateShipmentRequest> createValidator,
        IValidator<ChangeStatusRequest> changeStatusValidator,
        IValidator<AssignShipmentRequest> assignValidator)
    {
        _shipmentService = shipmentService;
        _createValidator = createValidator;
        _changeStatusValidator = changeStatusValidator;
        _assignValidator = assignValidator;
    }

    /// <summary>RF-01: crea un nuevo envío.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateShipmentRequest request)
    {
        await _createValidator.ValidateAndThrowAsync(request);
        var result = await _shipmentService.CreateAsync(request);
        return CreatedAtAction(nameof(GetByTrackingCode), new { trackingCode = result.TrackingCode }, result);
    }

    /// <summary>Consulta un envío por su código de rastreo.</summary>
    [HttpGet("{trackingCode}")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTrackingCode(string trackingCode)
    {
        var result = await _shipmentService.GetByTrackingCodeAsync(trackingCode);
        return Ok(result);
    }

    /// <summary>Lista envíos con filtros opcionales.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ShipmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ShipmentStatus? status,
        [FromQuery] string? originCity,
        [FromQuery] string? destinationCity)
    {
        var result = await _shipmentService.ListAsync(new ShipmentListFilter(status, originCity, destinationCity));
        return Ok(result);
    }

    /// <summary>RF-05: consulta envíos atrasados en un rango de fechas de creación.</summary>
    [HttpGet("delayed")]
    [ProducesResponseType(typeof(List<ShipmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDelayed([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (from > to)
            return BadRequest(new { message = "La fecha 'from' no puede ser posterior a 'to'." });

        var result = await _shipmentService.GetDelayedAsync(from, to);
        return Ok(result);
    }

    /// <summary>RF-02: cambia el estado de un envío (transición validada en el dominio).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusRequest request)
    {
        await _changeStatusValidator.ValidateAndThrowAsync(request);
        var result = await _shipmentService.ChangeStatusAsync(id, request);
        return Ok(result);
    }

    /// <summary>RF-03: asigna el envío a un conductor (y su vehículo), validando capacidad (RN-01).</summary>
    [HttpPost("{id:int}/assign")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignShipmentRequest request)
    {
        await _assignValidator.ValidateAndThrowAsync(request);
        var result = await _shipmentService.AssignAsync(id, request);
        return Ok(result);
    }
}
