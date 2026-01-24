using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Infrastructure;
using ECommerceMvp.Shared.Infrastructure;
using MongoDB.Driver;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB
var mongoOptions = new MongoDbOptions
{
    ConnectionString = builder.Configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017",
    DatabaseName = builder.Configuration["MongoDB:Database"] ?? "ecommerce"
};

var mongoClient = new MongoClient(mongoOptions.ConnectionString); builder.Services.AddSingleton(mongoClient); builder.Services.AddSingleton(mongoClient);
builder.Services.AddSingleton(mongoOptions);

// Register ProductProjectionWriter for dependency access
builder.Services.AddSingleton<IProductProjectionWriter>(sp =>
    new ProductProjectionWriter(mongoClient, mongoOptions.DatabaseName, sp.GetRequiredService<ILogger<ProductProjectionWriter>>()));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure Kestrel with flexible port binding
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var port = int.TryParse(Environment.GetEnvironmentVariable("ASPNETCORE_PORT"), out var p) ? p : 5001;
    serverOptions.ListenLocalhost(port);
});

var app = builder.Build();
app.MapControllers();
app.MapOpenApi();
app.Run();
