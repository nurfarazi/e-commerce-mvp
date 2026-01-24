using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Domain;
using ECommerceMvp.Cart.Infrastructure;
using MongoDB.Driver;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // MongoDB
        var mongoConnectionString = context.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
        var mongoClient = new MongoDB.Driver.MongoClient(mongoConnectionString);
        services.AddSingleton(mongoClient);

        // RabbitMQ
        var rabbitMqHostName = context.Configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        services.AddScoped<IEventPublisher>(sp =>
            new RabbitMqEventPublisher(rabbitMqHostName, "guest", "guest"));

        services.AddScoped<IEventStore>(sp =>
            new MongoEventStore(mongoClient.GetDatabase("ecommerce")));

        services.AddScoped<IIdempotencyStore>(sp =>
            new MongoIdempotencyStore(mongoClient.GetDatabase("ecommerce")));

        // Repository
        services.AddScoped<IRepository<ECommerceMvp.Cart.Domain.ShoppingCart, ECommerceMvp.Cart.Domain.CartId>>(sp =>
            new CartRepository(sp.GetRequiredService<IEventStore>(), sp.GetRequiredService<IEventPublisher>()));

        // Command Handlers
        services.AddScoped<ICommandHandler<CreateCartCommand, CreateCartResponse>, CreateCartCommandHandler>();
        services.AddScoped<ICommandHandler<AddCartItemCommand, AddCartItemResponse>, AddCartItemCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>, UpdateCartItemQtyCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>, RemoveCartItemCommandHandler>();
        services.AddScoped<ICommandHandler<ClearCartCommand, ClearCartResponse>, ClearCartCommandHandler>();

        // Worker
        services.AddHostedService<CartCommandWorker>();
    });

var host = builder.Build();
host.Run();
