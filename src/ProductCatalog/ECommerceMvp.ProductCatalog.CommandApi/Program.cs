using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Infrastructure;
using ECommerceMvp.Shared.Application;
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

// RabbitMQ
var rabbitMqOptions = new RabbitMqOptions
{
    HostName = builder.Configuration["RabbitMq:HostName"] ?? "localhost",
    Port = int.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672"),
    UserName = builder.Configuration["RabbitMq:UserName"] ?? "guest",
    Password = builder.Configuration["RabbitMq:Password"] ?? "guest"
};

builder.Services.AddSingleton(rabbitMqOptions);

// Shared infrastructure
builder.Services.AddSingleton<IEventStore, MongoEventStore>();
builder.Services.AddSingleton<IIdempotencyStore, MongoIdempotencyStore>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddSingleton<ICommandEnqueuer, RabbitMqCommandEnqueuer>();

builder.Services.AddControllers();

var app = builder.Build();

// Enable detailed error pages
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();
app.Run();
