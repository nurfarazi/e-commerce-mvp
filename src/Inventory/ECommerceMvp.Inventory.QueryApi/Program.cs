using ECommerceMvp.Inventory.Application;
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

var mongoClient = new MongoClient(mongoOptions.ConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoOptions);

// Query handlers
builder.Services.AddScoped(sp =>
    new GetStockAvailabilityQueryHandler(
        sp.GetRequiredService<IMongoClient>(),
        mongoOptions.DatabaseName,
        sp.GetRequiredService<ILogger<GetStockAvailabilityQueryHandler>>()));

builder.Services.AddScoped(sp =>
    new GetMultipleStockAvailabilityQueryHandler(
        sp.GetRequiredService<IMongoClient>(),
        mongoOptions.DatabaseName,
        sp.GetRequiredService<ILogger<GetMultipleStockAvailabilityQueryHandler>>()));

builder.Services.AddControllers();

var app = builder.Build();

// Enable detailed error pages
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();

Log.Information("Inventory QueryApi starting on port 5003");
app.Run();
