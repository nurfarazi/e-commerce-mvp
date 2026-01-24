using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.EventHandler;
using ECommerceMvp.ProductCatalog.Infrastructure;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using MongoDB.Driver;
using Serilog;
using Serilog.Events;

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
        services.AddSingleton<IIdempotencyStore, MongoIdempotencyStore>();

        // ProductCatalog services
        services.AddSingleton<IProductProjectionWriter>(provider =>
        {
            var mongoClient = provider.GetRequiredService<IMongoClient>();
            var mongoOptions = provider.GetRequiredService<MongoDbOptions>();
            var logger = provider.GetRequiredService<ILogger<ProductProjectionWriter>>();
            return new ProductProjectionWriter(mongoClient, mongoOptions.DatabaseName, logger);
        });

        // Worker
        services.AddHostedService<ProductEventWorker>();
    })
    .Build();

await host.RunAsync();
