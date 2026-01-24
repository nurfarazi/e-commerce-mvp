using ECommerceMvp.Checkout.EventHandler;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Checkout.Application;
using ECommerceMvp.Checkout.Infrastructure;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register core services
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        // Register Checkout services
        services.AddSingleton(typeof(IRepository<,>), typeof(CheckoutRepository));

        // Register command handlers (for AdvanceSaga command)
        services.AddScoped(typeof(ICommandHandler<,>), typeof(AdvanceSagaCommandHandler));

        // Register the event worker
        services.AddHostedService<CheckoutEventWorker>();
    })
    .Build();

await host.RunAsync();
