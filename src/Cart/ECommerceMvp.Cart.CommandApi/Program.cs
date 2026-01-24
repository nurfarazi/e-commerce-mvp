using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Domain;
using ECommerceMvp.Cart.Infrastructure;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Cart Repository & Handlers
builder.Services.AddScoped<IRepository<ShoppingCart, CartId>>(sp =>
    new CartRepository(sp.GetRequiredService<IEventStore>(), sp.GetRequiredService<IEventPublisher>()));

builder.Services.AddScoped<ICommandHandler<CreateCartCommand, CreateCartResponse>, CreateCartCommandHandler>();
builder.Services.AddScoped<ICommandHandler<AddCartItemCommand, AddCartItemResponse>, AddCartItemCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>, UpdateCartItemQtyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>, RemoveCartItemCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ClearCartCommand, ClearCartResponse>, ClearCartCommandHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
