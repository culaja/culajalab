FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY mqtt-timescale-consumer.csproj .
RUN dotnet restore mqtt-timescale-consumer.csproj

COPY . .
RUN dotnet build mqtt-timescale-consumer.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish mqtt-timescale-consumer.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "mqtt-timescale-consumer.dll"]
