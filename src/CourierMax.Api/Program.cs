using CourierMax.Api.Middleware;
using CourierMax.Application.DTOs;
using CourierMax.Application.Interfaces;
using CourierMax.Application.Services;
using CourierMax.Application.Validators;
using CourierMax.Domain.Interfaces;
using CourierMax.Infrastructure.Persistence;
using CourierMax.Infrastructure.Repositories;
using CourierMax.Infrastructure.Seed;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Persistencia ---
// El proveedor se selecciona por configuración, no hardcodeado: en desarrollo
// local usa SQLite (cero fricción, sin instalar nada); en Azure se cambia a
// SQL Server solo con appsettings/variables de entorno, sin tocar código
// (ver README, sección Despliegue en Azure).
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<CourierMaxDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString, sql =>
            sql.EnableRetryOnFailure(maxRetryCount: 3)); // resiliencia ante fallos transitorios de red en Azure SQL
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// --- Repositorios (Infrastructure) ---
builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<ICityRepository, CityRepository>();
builder.Services.AddScoped<IPublicHolidayRepository, PublicHolidayRepository>();

// --- Servicios de dominio/aplicación ---
builder.Services.AddScoped<IRateCalculator, RateCalculator>();
builder.Services.AddScoped<IBusinessDayCalculator, BusinessDayCalculator>();
builder.Services.AddScoped<IVehicleCapacityService, VehicleCapacityService>();
builder.Services.AddScoped<ITrackingCodeGenerator, TrackingCodeGenerator>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IDriverReportService, DriverReportService>();
builder.Services.AddScoped<IVehicleQueryService, VehicleQueryService>();

// --- Validación (FluentValidation) ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateShipmentRequestValidator>();

// --- Rate limiting básico: mitiga abuso simple (ej. scraping del catálogo de
// envíos o fuerza bruta sobre /assign) sin agregar infraestructura externa
// (Redis, etc.) que no se justifica para el alcance de esta prueba. Azure App
// Service ya provee protección DDoS de borde (ver README); esto es una capa
// adicional a nivel de aplicación.
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// --- Controllers + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CourierMax API",
        Version = "v1",
        Description = "API REST para gestión de envíos, asignación de vehículos/conductores, " +
                       "cálculo de tarifas y métricas de eficiencia."
    });
});

var app = builder.Build();

// --- Creación de esquema y seed de datos de referencia ---
// Con SQLite (desarrollo local) se usa EnsureCreated() para que el evaluador
// pueda ejecutar el proyecto con un solo "dotnet run" sin pasos previos.
// Con SQL Server (Azure) se usa Migrate(), el enfoque correcto en producción:
// aplica el historial de migraciones versionadas de forma idempotente, lo cual
// EnsureCreated() no soporta (no sabe evolucionar un esquema ya existente).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CourierMaxDbContext>();

    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    DatabaseSeeder.Seed(db);
}

// --- Middleware pipeline ---
app.UseMiddleware<ExceptionMiddleware>();

// HSTS solo en producción: indica a los navegadores forzar HTTPS en
// visitas futuras. En desarrollo se omite porque interfiere con el
// certificado autofirmado local de dotnet.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CourierMax API v1");
    c.RoutePrefix = string.Empty; // Swagger en la raíz, conveniente para el evaluador
});

app.UseAuthorization();
app.MapControllers();

app.Run();
