using Serilog;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Services;
using LocalSync.Infrastructure.Discovery;
using LocalSync.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

// Register LocalSync Core & Infrastructure services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDeviceManager, DeviceManager>();
builder.Services.AddSingleton<IDeviceDiscoveryService, MdnsDiscoveryService>();
builder.Services.AddSingleton<ITransferNotificationService, SignalRNotificationService>();
builder.Services.AddSingleton<ITransferManager, TransferManager>();
builder.Services.AddSingleton<ITransferClient, LocalSync.Infrastructure.Networking.TransferClient>();
builder.Services.AddHostedService<DiscoveryHostedService>();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Add Health Checks endpoint
app.MapHealthChecks("/health");

app.MapControllers();
app.MapHub<LocalSync.Api.Hubs.TransferHub>("/transferHub");
app.MapGet("/", () => "LocalSync API is running!");

app.Run();
