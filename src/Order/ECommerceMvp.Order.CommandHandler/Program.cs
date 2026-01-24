using ECommerceMvp.Order.CommandHandler;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Application;
using ECommerceMvp.Order.Infrastructure;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register services
        services.AddSingleton(typeof(IRepository<,>), typeof(InMemoryOrderRepository));
        services.AddSingleton(typeof(IEventStore), typeof(InMemoryOrderEventStore));
        services.AddSingleton(typeof(IIdempotencyStore), typeof(InMemoryIdempotencyStore));
        
        // Register command handlers
        services.AddScoped(typeof(ICommandHandler<,>), typeof(PlaceOrderCommandHandler));

        services.AddHostedService<OrderCommandWorker>();
    })
    .Build();

await host.RunAsync();
