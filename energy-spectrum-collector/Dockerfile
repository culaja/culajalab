FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY energy-spectrum-collector.csproj .
RUN dotnet restore energy-spectrum-collector.csproj

COPY . .
RUN dotnet build energy-spectrum-collector.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish energy-spectrum-collector.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "energy-spectrum-collector.dll"]
