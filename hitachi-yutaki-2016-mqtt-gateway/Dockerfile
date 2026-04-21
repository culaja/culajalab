FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY hitachi-yutaki-2016-mqtt-gateway.csproj .
RUN dotnet restore hitachi-yutaki-2016-mqtt-gateway.csproj

COPY . .
RUN dotnet build hitachi-yutaki-2016-mqtt-gateway.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish hitachi-yutaki-2016-mqtt-gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "hitachi-yutaki-2016-mqtt-gateway.dll"]
