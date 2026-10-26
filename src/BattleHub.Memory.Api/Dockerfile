# Se construye desde la raíz del repo (docker-compose.yml), porque necesita
# Directory.Build.props y los proyectos Domain y Data.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Primero solo los .csproj: el restore queda en caché mientras no cambien.
COPY Directory.Build.props ./
COPY src/BattleHub.Memory.Domain/BattleHub.Memory.Domain.csproj src/BattleHub.Memory.Domain/
COPY src/BattleHub.Memory.Data/BattleHub.Memory.Data.csproj src/BattleHub.Memory.Data/
COPY src/BattleHub.Memory.Api/BattleHub.Memory.Api.csproj src/BattleHub.Memory.Api/
RUN dotnet restore src/BattleHub.Memory.Api/BattleHub.Memory.Api.csproj

COPY src/BattleHub.Memory.Domain/ src/BattleHub.Memory.Domain/
COPY src/BattleHub.Memory.Data/ src/BattleHub.Memory.Data/
COPY src/BattleHub.Memory.Api/ src/BattleHub.Memory.Api/
RUN dotnet publish src/BattleHub.Memory.Api/BattleHub.Memory.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# La imagen escucha en el puerto 8080.
ENTRYPOINT ["dotnet", "BattleHub.Memory.Api.dll"]
