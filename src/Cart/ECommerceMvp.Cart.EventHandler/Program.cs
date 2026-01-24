using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Infrastructure;
using MongoDB.Driver;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // MongoDB
        var mongoConnectionString = context.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
        var mongoClient = new MongoDB.Driver.MongoClient(mongoConnectionString);
        services.AddSingleton(mongoClient);

        // Projection Writer
        services.AddScoped<ICartProjectionWriter>(sp =>
            new CartProjectionWriter(mongoClient.GetDatabase("ecommerce")));

        // Worker
        services.AddHostedService<CartEventWorker>();
    });

var host = builder.Build();
host.Run();
