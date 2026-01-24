var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
var mongoClient = new MongoDB.Driver.MongoClient(mongoConnectionString);
builder.Services.AddSingleton(mongoClient);

// Query handler
builder.Services.AddScoped<IQueryHandler<GetCartByGuestTokenQuery, CartView?>, GetCartByGuestTokenQueryHandler>();

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
