# CourierMax API

API REST para la gestión del ciclo de vida de envíos de CourierMax: creación, asignación a vehículo/conductor, seguimiento de estados, cálculo de tarifas, alertas de SLA y reporte de métricas por conductor.

## Tecnologías

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8 (SQLite)
- FluentValidation
- xUnit + Moq + FluentAssertions
- Swagger / OpenAPI (Swashbuckle)

## Cómo ejecutar el proyecto localmente

### Requisitos
- .NET 8 SDK ([descarga aquí](https://dotnet.microsoft.com/download/dotnet/8.0))

### Pasos

```bash
git clone https://github.com/JoseMMC99/CourierMax
cd CourierMax

dotnet restore
dotnet build

cd src/CourierMax.Api
dotnet run
```

La API queda disponible en `https://localhost:5001` (o el puerto que indique la consola). Swagger UI se sirve en la raíz (`/`), así que abrir esa URL en el navegador es suficiente para explorar y probar todos los endpoints.

La base de datos SQLite (`couriermax.db`) se crea automáticamente al iniciar la aplicación por primera vez (vía `EnsureCreated()`), junto con el seed de datos de referencia: las 4 ciudades, las 6 rutas con sus tarifas de distancia, los festivos colombianos 2026, y los 3 vehículos/conductores de ejemplo del enunciado.

El proveedor de base de datos es configurable sin tocar código, vía `Database:Provider` en `appsettings.json` (o variable de entorno `Database__Provider`): `Sqlite` (default, desarrollo local) o `SqlServer` (Azure SQL, producción). Con `SqlServer` la aplicación usa `Migrate()` en lugar de `EnsureCreated()`, lo que requiere generar las migraciones una sola vez antes del primer despliegue (ver sección **Despliegue en Azure** más abajo para el paso a paso completo):

```bash
cd src/CourierMax.Api
dotnet ef migrations add InitialCreate --project ../CourierMax.Infrastructure --startup-project .
```

### Correr los tests

```bash
dotnet test
```

## Arquitectura

El proyecto sigue **Clean Architecture** en 4 capas, con dependencias apuntando siempre hacia el dominio:

```
CourierMax.Api            → controllers, middleware, configuración (DI, Swagger)
        ↓ depende de
CourierMax.Infrastructure → EF Core, DbContext, repositorios concretos, seed de datos
        ↓ depende de
CourierMax.Application    → casos de uso (services), DTOs, validadores, interfaces de servicios
        ↓ depende de
CourierMax.Domain         → entidades, value objects, excepciones de dominio, interfaces de repos
```

`CourierMax.Domain` no depende de ningún otro proyecto es código C# puro, sin referencias a EF Core ni a ningún framework. Esto permite testear toda la lógica de negocio crítica (máquina de estados, cálculo de tarifas, validación de capacidad) sin necesidad de una base de datos.

### Por qué Clean Architecture y no CQRS/MediatR

Con 5-6 requerimientos funcionales y un alcance calibrado a ~12h de desarrollo, introducir MediatR con un Command/Handler/Validator por cada operación agrega ceremonia sin un beneficio real: no hay necesidad de desacoplar lecturas de escrituras a este tamaño. Se optó por **Application Services con interfaces** (`IShipmentService`, `IDriverReportService`, etc.) inyectados por DI, que son más legibles para un evaluador y más fáciles de testear con mocks simples. CQRS es una herramienta conocida que aquí se decidió no usar porque no aporta valor proporcional a su costo esa es la señal de juicio técnico que se buscó priorizar sobre la familiaridad con el patrón.

### Por qué EF Core + SQLite y no Dapper o in-memory

SQLite con EF Core Code-First da lo mejor de ambos mundos para una prueba técnica: un ORM real con tracking de cambios y LINQ (más representativo de un proyecto productivo que un diccionario en memoria), pero sin la fricción de que el evaluador tenga que instalar y configurar SQL Server para correr el proyecto. El cambio a SQL Server es una sola línea (connection string + `UseSqlServer` en lugar de `UseSqlite`).

### Patrones aplicados

- **Repository pattern** sobre interfaces definidas en `Domain` (`IShipmentRepository`, etc.), implementadas en `Infrastructure`. Aísla el dominio y la capa de aplicación de los detalles de EF Core, y permite mockear los repos en tests unitarios de servicios sin tocar una base de datos real.
- **Domain exceptions** (`VehicleCapacityExceededException`, `InvalidStatusTransitionException`, etc.) en lugar de retornar `null`/`bool`/códigos de error. Un middleware centralizado (`ExceptionMiddleware`) las traduce a códigos HTTP apropiados (404, 409, 400), evitando try/catch repetidos en cada controller.
- **Strategy implícita** en `IRateCalculator`: la tarifa base varía según `ServiceType`, pero los recargos (peso, distancia, tipo de paquete) son políticas compartidas que no se modelaron como una Strategy separada por recargo habría sido indirección sin beneficio (no varían por ningún eje adicional).
- **Value Objects** (`ContactInfo`, `PackageInfo`, `TrackingCode`) para encapsular invariantes de datos que siempre deben viajar juntos y validarse como unidad.
- **Result pattern NO se usó.** Las excepciones de dominio + middleware centralizado ya dan manejo de errores claro, y son el enfoque más estándar en un equipo .NET típico; agregar un wrapper `Result<T>` habría sido una capa de indirección adicional sin justificación clara para este alcance.


## Estructura del proyecto

```
CourierMax.sln
├── .github/workflows/
│   └── azure-deploy.yml        (CI/CD: build, test, deploy a Azure App Service)
├── Dockerfile                  (multi-stage, usuario no-root)
├── src/
│   ├── CourierMax.Domain/
│   │   ├── Entities/         (Shipment, StatusChange, Vehicle, Driver, City, CityDistance, PublicHoliday)
│   │   ├── ValueObjects/      (ContactInfo, PackageInfo, TrackingCode)
│   │   ├── Enums/
│   │   ├── Exceptions/        (excepciones de dominio)
│   │   └── Interfaces/        (interfaces de repositorios)
│   ├── CourierMax.Application/
│   │   ├── DTOs/
│   │   ├── Services/           (ShipmentService, RateCalculator, BusinessDayCalculator, VehicleCapacityService, DriverReportService, etc.)
│   │   ├── Interfaces/         (interfaces de servicios)
│   │   └── Validators/         (FluentValidation)
│   ├── CourierMax.Infrastructure/
│   │   ├── Persistence/        (CourierMaxDbContext)
│   │   ├── Repositories/       (implementaciones concretas, incl. proyecciones SQL de RF-06)
│   │   └── Seed/               (datos de referencia del enunciado)
│   └── CourierMax.Api/
│       ├── Controllers/
│       ├── Middleware/         (ExceptionMiddleware)
│       ├── appsettings.json              (config local, SQLite)
│       ├── appsettings.Production.json   (plantilla Azure, sin secretos)
│       └── Program.cs
└── tests/
    ├── CourierMax.Domain.Tests/        (Shipment, Vehicle, lógica pura sin mocks)
    └── CourierMax.Application.Tests/   (RateCalculator, BusinessDayCalculator, VehicleCapacityService, DriverReportService, Validators, con Moq)
```

## Seguridad

Medidas implementadas, con el lugar exacto del código donde se aplican:

- **Prevención de SQL Injection, por diseño, no por sanitización manual.** Todo el acceso a datos pasa por LINQ-to-EF Core (ver `src/CourierMax.Infrastructure/Repositories/`); no hay una sola línea de SQL crudo, interpolado o concatenado en todo el proyecto. Cuando EF Core traduce una expresión como `_context.Shipments.Where(s => s.TrackingCode == trackingCode)` a SQL, genera automáticamente una consulta parametrizada (`WHERE TrackingCode = @p0`), nunca concatena el valor del usuario directamente en el texto del query. Esto es válido incluso para la búsqueda case-insensitive en `CityRepository.GetByNameAsync` (`c.Name.ToLower() == name.ToLower()`), que sigue siendo una comparación parametrizada.

- **Validación de entrada en capas (defensa en profundidad).** Cada request pasa por FluentValidation (`src/CourierMax.Application/Validators/Validators.cs`) antes de llegar a cualquier lógica de negocio: formato de teléfono colombiano vía regex (`^[36]\d{9}$`), rangos numéricos (peso 0.1-100kg, dimensiones 1-200cm), y **límites de longitud máxima en todos los campos de texto** (ej. `MaximumLength(300)` en direcciones) que coinciden exactamente con los `HasMaxLength` configurados en `CourierMaxDbContext`. Esto evita que un payload con un string desproporcionado llegue a EF Core y falle recién al guardar con un error 500 confuso , el rechazo ocurre antes, con un 400 claro.

- **Manejo centralizado de errores que no filtra información interna.** `ExceptionMiddleware` (`src/CourierMax.Api/Middleware/ExceptionMiddleware.cs`) garantiza que cualquier excepción no anticipada devuelva siempre un mensaje genérico fijo ("Ocurrió un error interno inesperado") con HTTP 500, sin exponer `exception.Message`, stack trace, nombres de tabla o rutas de archivo al cliente. El detalle completo de la excepción solo se escribe en el log del servidor (`_logger.LogError`), nunca en la respuesta HTTP. Solo las excepciones de dominio que se definieron a propósito (con mensajes ya controlados y sin datos sensibles del sistema) llegan al cliente, vía 404/409/400 según corresponda.

- **HTTPS forzado y HSTS en producción.** `app.UseHttpsRedirection()` redirige cualquier tráfico HTTP a HTTPS; `app.UseHsts()` (activo solo fuera de `Development`) instruye a los navegadores a recordar usar HTTPS en visitas futuras, mitigando ataques de downgrade.

- **Rate limiting a nivel de aplicación.** Se configuró un límite de 100 requests por minuto por IP (`builder.Services.AddRateLimiter` en `Program.cs`), como mitigación básica contra abuso simple (scraping del catálogo de envíos, fuerza bruta sobre `/assign`) sin necesitar infraestructura externa como Redis. Azure App Service ya añade protección DDoS de borde por encima de esto.

- **Sin secretos en el repositorio.** La connection string de Azure SQL **nunca** se versiona: `appsettings.Production.json` contiene un placeholder explícito (`__SET_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT__`) en lugar de un valor real. El valor verdadero se inyecta vía Application Settings de Azure App Service (variables de entorno) o Azure Key Vault, ver sección de despliegue. `appsettings.Development.json` está además excluido en `.gitignore` para que un desarrollador no suba accidentalmente una connection string local con credenciales.

- **Contenedor Docker ejecutado como usuario no-root.** El `Dockerfile` crea un usuario sin privilegios (`adduser --disabled-password`) y cambia a él con `USER appuser` antes de ejecutar la aplicación, de forma que si se explotara una vulnerabilidad de la app dentro del contenedor, el atacante no obtiene privilegios de root del sistema operativo del contenedor.

- **Validación de formato en Value Objects del dominio**, como última línea de defensa independiente de la capa HTTP: `TrackingCode` (`src/CourierMax.Domain/ValueObjects/ValueObjects.cs`) valida su propio formato en el constructor y lanza si no cumple `CM-XXXXXXXX`, sin importar qué capa lo esté construyendo.


## Despliegue en Azure (App Service + Azure SQL)

### Arquitectura de despliegue

```
GitHub (push a main)
      ↓
GitHub Actions (.github/workflows/azure-deploy.yml)
      ↓ build + test (el deploy se cancela si algún test falla)
      ↓ dotnet publish
      ↓
Azure App Service  ←────────→  Azure SQL Database
(Linux, .NET 8 runtime)         (servidor + base de datos)
```

### Paso 1, Crear los recursos en Azure

Usando Azure CLI (`az login` primero):

```bash
# Variables (ajustar nombres; deben ser únicos globalmente en Azure)
RESOURCE_GROUP="couriermax-rg"
LOCATION="eastus"
SQL_SERVER_NAME="couriermax-sqlserver"
SQL_DB_NAME="couriermax-db"
SQL_ADMIN_USER="couriermaxadmin"
APP_SERVICE_PLAN="couriermax-plan"
WEBAPP_NAME="couriermax-api"

# Resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Azure SQL Server + base de datos (tier Basic, suficiente para esta prueba)
az sql server create \
  --name $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN_USER \
  --admin-password "<una-contraseña-segura-aquí>"

az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name $SQL_DB_NAME \
  --service-objective Basic

# Permitir que servicios de Azure (el propio App Service) accedan al servidor SQL
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

# App Service Plan (Linux, tier B1 , básico, económico)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --is-linux --sku B1

# Web App configurada para correr el contenedor Docker del proyecto
az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --name $WEBAPP_NAME \
  --deployment-container-image-name mcr.microsoft.com/dotnet/aspnet:8.0
```

> La contraseña de SQL **nunca** se escribe en un script versionado en el repo; en este ejemplo de comandos se ejecuta manualmente con la contraseña real, o se pasa como variable de entorno desde una terminal local que no se commitea.

### Paso 2 , Configurar la connection string en App Service (sin secretos en el repo)

```bash
CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Database=${SQL_DB_NAME};User ID=${SQL_ADMIN_USER};Password=<contraseña-real>;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"

az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    Database__Provider="SqlServer" \
    ConnectionStrings__DefaultConnection="$CONNECTION_STRING" \
    WEBSITES_PORT="8080"
```

`ConnectionStrings__DefaultConnection` (con doble guion bajo) es la convención de ASP.NET Core para mapear variables de entorno a la configuración jerárquica , equivale a `ConnectionStrings:DefaultConnection` en `appsettings.json`, pero inyectado en runtime sin tocar ningún archivo versionado. Para un entorno productivo real, el paso recomendado es usar **Azure Key Vault** + referencias de Key Vault en App Settings, en lugar de texto plano en App Settings; se documenta aquí la alternativa más simple porque cubre el requisito del puesto sin sobre-ingeniería para una prueba técnica.

### Paso 3 , Generar las migraciones de EF Core (una sola vez, antes del primer despliegue)

Como se usa `Migrate()` con SQL Server (ver justificación en `Program.cs`), las migraciones deben generarse localmente y viajar en el repositorio:

```bash
cd src/CourierMax.Api
dotnet ef migrations add InitialCreate --project ../CourierMax.Infrastructure --startup-project .
```

Esto crea la carpeta `src/CourierMax.Infrastructure/Migrations/` con el snapshot del esquema. Se debe commitear al repositorio , `Migrate()` la lee en cada arranque de la app para aplicar cambios pendientes de forma idempotente.

### Paso 4 , Desplegar

**Opción A , manual (rápida, para una primera verificación):**

```bash
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $WEBAPP_NAME \
  --src-path ./publish.zip \
  --type zip
```

**Opción B , CI/CD automático (la forma correcta para el día a día):**

El workflow en `.github/workflows/azure-deploy.yml` ya está configurado para hacer build, correr todos los tests, y desplegar a Azure App Service en cada push a `main` , **el despliegue se cancela automáticamente si algún test falla**. Solo falta:

1. En Azure Portal, ir al App Service → **Overview** → **Get publish profile**, descargar el archivo.
2. En GitHub, ir a **Settings → Secrets and variables → Actions → New repository secret**, crear `AZURE_WEBAPP_PUBLISH_PROFILE` con el contenido completo de ese archivo.
3. Ajustar `AZURE_WEBAPP_NAME` en el workflow al nombre real del App Service.
4. Hacer push a `main` , el pipeline corre automáticamente.

### Despliegue alternativo vía contenedor Docker

El `Dockerfile` en la raíz del repo es multi-stage (build con el SDK completo, runtime final solo con ASP.NET, sin SDK) y corre como usuario no-root. Para desplegarlo como contenedor en lugar de código nativo:

```bash
docker build -t couriermax-api:latest .
docker run -p 8080:8080 -e Database__Provider=Sqlite couriermax-api:latest
```

Y en Azure, apuntando el App Service a una imagen en Azure Container Registry o Docker Hub en lugar del runtime nativo , el `WEBSITES_PORT=8080` configurado arriba ya coincide con el puerto expuesto en el Dockerfile.


### Crear un envío

```bash
curl -X POST https://localhost:5001/api/shipments \
  -H "Content-Type: application/json" \
  -d '{
    "senderName": "Juan Pérez",
    "senderPhone": "3001234567",
    "senderAddress": "Calle 1 #2-3, Bogotá",
    "recipientName": "Ana Gómez",
    "recipientPhone": "6012345678",
    "recipientAddress": "Av Siempre Viva 742, Medellín",
    "packageWeightKg": 5,
    "packageLengthCm": 30,
    "packageWidthCm": 20,
    "packageHeightCm": 15,
    "packageType": 2,
    "serviceType": 1,
    "originCity": "Bogotá",
    "destinationCity": "Medellín"
  }'
```

> `packageType`: 0=Documento, 1=Paquete, 2=Fragil, 3=Perecedero
> `serviceType`: 0=Estandar, 1=Express, 2=MismoDia

Respuesta esperada (201 Created): el envío con `cost: 40950` (coincide con el ejemplo del enunciado: frágil, 5kg, express, Bogotá-Medellín).

### Consultar por código de rastreo

```bash
curl https://localhost:5001/api/shipments/CM-00000001
```

### Asignar a un conductor

```bash
curl -X POST https://localhost:5001/api/shipments/1/assign \
  -H "Content-Type: application/json" \
  -d '{ "driverId": 1 }'
```

### Cambiar estado

```bash
curl -X PATCH https://localhost:5001/api/shipments/1/status \
  -H "Content-Type: application/json" \
  -d '{ "newStatus": 2, "changedByUserId": "user1" }'
```

> `newStatus`: 1=Asignado, 2=EnTransito, 3=Entregado, 4=Cancelado

### Cancelar (requiere motivo ≥5 caracteres)

```bash
curl -X PATCH https://localhost:5001/api/shipments/1/status \
  -H "Content-Type: application/json" \
  -d '{ "newStatus": 4, "changedByUserId": "user1", "reason": "Cliente canceló el pedido" }'
```

### Consultar envíos atrasados

```bash
curl "https://localhost:5001/api/shipments/delayed?from=2026-06-01&to=2026-06-30"
```

### Reporte de métricas por conductor

```bash
curl https://localhost:5001/api/drivers/1/report
```

Para una exploración completa e interactiva de todos los endpoints, usar Swagger UI en la raíz del proyecto al ejecutarlo localmente.

## Estrategia de testing

- **`CourierMax.Domain.Tests`**: lógica pura del agregado `Shipment` (máquina de estados, cancelación, detección de atraso) y de `Vehicle` (validación de capacidad), sin mocks ni dependencias externas.
- **`CourierMax.Application.Tests`**: servicios de aplicación con repositorios mockeados vía Moq (`RateCalculator`, `BusinessDayCalculator`, `VehicleCapacityService`), y validadores de FluentValidation.
- El test `Calculate_EjemploExactoDelEnunciado_RetornaCostoEsperado` reproduce exactamente el ejemplo numérico del enunciado (paquete frágil 5kg, express, Bogotá-Medellín = $40,950) como verificación de regresión directa contra el documento de requerimientos.
- El test `CalculateSlaDeadline_ViernesMasUnDiaHabil_RetornaLunes` valida el caso explícito de RN-02 (un viernes + días hábiles no debe contar fin de semana).
