# syntax=docker/dockerfile:1

# --- Etapa de build: usa el SDK completo (pesado) solo para compilar ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo los .csproj primero para aprovechar el cache de capas de Docker:
# si el código cambia pero las dependencias no, este paso no se repite.
COPY src/CourierMax.Domain/CourierMax.Domain.csproj src/CourierMax.Domain/
COPY src/CourierMax.Application/CourierMax.Application.csproj src/CourierMax.Application/
COPY src/CourierMax.Infrastructure/CourierMax.Infrastructure.csproj src/CourierMax.Infrastructure/
COPY src/CourierMax.Api/CourierMax.Api.csproj src/CourierMax.Api/
RUN dotnet restore src/CourierMax.Api/CourierMax.Api.csproj

# Ahora sí, copiar el resto del código y compilar en Release.
COPY src/ src/
RUN dotnet publish src/CourierMax.Api/CourierMax.Api.csproj -c Release -o /app/publish --no-restore

# --- Etapa final: solo el runtime de ASP.NET (mucho más liviana, sin SDK) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Ejecutar como usuario no-root: si un atacante explota una vulnerabilidad de
# la app, no obtiene privilegios de root dentro del contenedor.
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# Azure App Service for Containers espera que el contenedor escuche en el
# puerto indicado por la variable de entorno WEBSITES_PORT (por defecto 80).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CourierMax.Api.dll"]
