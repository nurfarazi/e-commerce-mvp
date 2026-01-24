using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.CommandHandler;
using ECommerceMvp.Cart.Domain;
using ECommerceMvp.Cart.Infrastructure;
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
        services.AddSingleton<ICommandEnqueuer, RabbitMqCommandEnqueuer>();

        // Cart Repository & Handlers
        services.AddScoped<IRepository<ShoppingCart, CartId>>(sp =>
            new CartRepository(sp.GetRequiredService<IEventStore>(), sp.GetRequiredService<IEventPublisher>()));

        services.AddScoped<ICommandHandler<CreateCartCommand, CreateCartResponse>, CreateCartCommandHandler>();
        services.AddScoped<ICommandHandler<AddCartItemCommand, AddCartItemResponse>, AddCartItemCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>, UpdateCartItemQtyCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>, RemoveCartItemCommandHandler>();
        services.AddScoped<ICommandHandler<ClearCartCommand, ClearCartResponse>, ClearCartCommandHandler>();

        // Worker
        services.AddHostedService<CartCommandWorker>();
    })
    .Build();

Log.Information("Cart CommandHandler starting on port 5004");
await host.RunAsync();

