using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Domain;
using ECommerceMvp.Cart.Infrastructure;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
var mongoClient = new MongoDB.Driver.MongoClient(mongoConnectionString);
builder.Services.AddSingleton(mongoClient);

// RabbitMQ
var rabbitMqHostName = builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost";
builder.Services.AddScoped<ICommandEnqueuer>(sp =>
    new RabbitMqCommandEnqueuer(rabbitMqHostName, "guest", "guest"));

builder.Services.AddScoped<IEventPublisher>(sp =>
    new RabbitMqEventPublisher(rabbitMqHostName, "guest", "guest"));

builder.Services.AddScoped<IEventStore>(sp =>
    new MongoEventStore(mongoClient.GetDatabase("ecommerce")));

builder.Services.AddScoped<IIdempotencyStore>(sp =>
    new MongoIdempotencyStore(mongoClient.GetDatabase("ecommerce")));

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
