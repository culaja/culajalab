FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ha-device-registrar.csproj .
RUN dotnet restore ha-device-registrar.csproj

COPY . .
RUN dotnet build ha-device-registrar.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ha-device-registrar.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ha-device-registrar.dll"]
