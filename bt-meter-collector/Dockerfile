FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY bt-meter-collector.csproj .
RUN dotnet restore bt-meter-collector.csproj

COPY . .
RUN dotnet build bt-meter-collector.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish bt-meter-collector.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "bt-meter-collector.dll"]
