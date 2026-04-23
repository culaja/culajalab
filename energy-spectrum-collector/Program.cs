using energy_spectrum_collector;

var builder = Host.CreateApplicationBuilder(args);

var cfg = builder.Configuration.GetSection("EnergySpectrum").Get<EnergySpectrumConfiguration>()
    ?? throw new InvalidOperationException("Missing EnergySpectrum configuration");
var connectionString = builder.Configuration.GetConnectionString("TimescaleDb")
    ?? throw new InvalidOperationException("Missing TimescaleDb connection string");

builder.Services.AddSingleton(cfg);
builder.Services.AddSingleton(new DbConnectionString(connectionString));
builder.Services.AddHttpClient<Worker>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
