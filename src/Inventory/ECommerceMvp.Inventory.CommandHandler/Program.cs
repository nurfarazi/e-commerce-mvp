using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Inventory.CommandHandler;
using ECommerceMvp.Inventory.Infrastructure;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using MongoDB.Driver;
using Serilog;

// Logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        // MongoDB
        var mongoOptions = new MongoDbOptions
        {
            ConnectionString = context.Configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017",
            DatabaseName = context.Configuration["MongoDB:Database"] ?? "ecommerce"
        };

        var mongoClient = new MongoClient(mongoOptions.ConnectionString);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(mongoOptions);

        // RabbitMQ
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = context.Configuration["RabbitMq:HostName"] ?? "localhost",
            Port = int.Parse(context.Configuration["RabbitMq:Port"] ?? "5672"),
            UserName = context.Configuration["RabbitMq:UserName"] ?? "guest",
            Password = context.Configuration["RabbitMq:Password"] ?? "guest"
        };

        services.AddSingleton(rabbitMqOptions);

        // Shared infrastructure
        services.AddSingleton<IEventStore, MongoEventStore>();
        services.AddSingleton<IIdempotencyStore, MongoIdempotencyStore>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        // Inventory services
        services.AddScoped<IRepository<ECommerceMvp.Inventory.Domain.InventoryItem, string>, InventoryRepository>();
        services.AddScoped<ICommandHandler<SetStockCommand, SetStockResponse>, SetStockCommandHandler>();
        services.AddScoped<ICommandHandler<ValidateStockCommand, ValidateStockResponse>, ValidateStockCommandHandler>();
        services.AddScoped<ICommandHandler<DeductStockForOrderCommand, DeductStockForOrderResponse>, DeductStockForOrderCommandHandler>();

        // Worker
        services.AddHostedService<InventoryCommandWorker>();
    })
    .Build();

await host.RunAsync();
