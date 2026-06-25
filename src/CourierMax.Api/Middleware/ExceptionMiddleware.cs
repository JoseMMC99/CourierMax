using System.Net;
using System.Text.Json;
using CourierMax.Domain.Exceptions;
using FluentValidation;

namespace CourierMax.Api.Middleware;

/// <summary>
/// Manejo centralizado de errores (requisito técnico del enunciado).
/// Mapea excepciones de dominio/validación a códigos HTTP apropiados, en lugar
/// de dejar que cada controller tenga su propio try/catch repetido.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = MapException(exception);

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Error no controlado procesando {Path}", context.Request.Path);
        else
            _logger.LogWarning("{ExceptionType}: {Message}", exception.GetType().Name, exception.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new
        {
            statusCode = (int)statusCode,
            message,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    /// <summary>
    /// Traduce excepciones a (status code, mensaje público, lista de errores).
    /// Importante en términos de seguridad: la rama catch-all (_) NUNCA expone
    /// exception.Message ni stack trace al cliente — solo un mensaje genérico
    /// fijo. Esto evita filtrar detalles internos (rutas de archivo, nombres de
    /// tabla, versión de librerías) que podrían ayudar a un atacante a mapear
    /// la infraestructura interna. El detalle completo solo va al log del
    /// servidor (LogError), nunca a la respuesta HTTP.
    /// </summary>
    private static (HttpStatusCode StatusCode, string Message, IEnumerable<string>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            ShipmentNotFoundException notFound =>
                (HttpStatusCode.NotFound, notFound.Message, null),

            ValidationException validationEx =>
                (HttpStatusCode.BadRequest, "Error de validación.",
                    validationEx.Errors.Select(e => e.ErrorMessage)),

            DomainException domainEx =>
                (HttpStatusCode.Conflict, domainEx.Message, null),

            ArgumentException argEx =>
                (HttpStatusCode.BadRequest, argEx.Message, null),

            InvalidOperationException invalidOpEx =>
                (HttpStatusCode.BadRequest, invalidOpEx.Message, null),

            _ =>
                (HttpStatusCode.InternalServerError, "Ocurrió un error interno inesperado.", null)
        };
    }
}
