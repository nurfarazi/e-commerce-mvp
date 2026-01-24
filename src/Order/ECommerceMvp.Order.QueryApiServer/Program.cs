using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Order.QueryApi;

namespace ECommerceMvp.Order.QueryApiServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplicationBuilder.CreateBuilder(args);

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddSwaggerGen();
        builder.Services.AddLogging();

        // Register core services
        builder.Services.AddSingleton<IQueryBus, QueryBus>();

        // Register Order query services
        builder.Services.AddSingleton<IReadModelStore<OrderDetailView>, InMemoryOrderDetailReadModelStore>();
        builder.Services.AddSingleton<IReadModelStore<AdminOrderListView>, InMemoryAdminOrderListReadModelStore>();

        // Register query handlers
        builder.Services.AddScoped<IQueryHandler<GetOrderDetailQuery, OrderDetailView?>, GetOrderDetailQueryHandler>();
        builder.Services.AddScoped<IQueryHandler<GetOrderDetailByNumberQuery, OrderDetailView?>, GetOrderDetailByNumberQueryHandler>();
        builder.Services.AddScoped<IQueryHandler<GetAllOrdersAdminQuery, List<AdminOrderListView>>, GetAllOrdersAdminQueryHandler>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
