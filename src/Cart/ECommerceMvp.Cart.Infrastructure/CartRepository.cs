using ECommerceMvp.Shared.Application;
using ECommerceMvp.Cart.Domain;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.Json;

namespace ECommerceMvp.Cart.Infrastructure;

/// <summary>
/// Event Sourcing Repository for ShoppingCart aggregate
/// Persists events to MongoDB event store with optimistic concurrency
/// </summary>
public class CartRepository : IRepository<ShoppingCart, CartId>
{
    private readonly IEventStore _eventStore;
    private readonly IEventPublisher _eventPublisher;

    public CartRepository(IEventStore eventStore, IEventPublisher eventPublisher)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<ShoppingCart?> GetByIdAsync(CartId id)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        var streamId = $"cart-{id}";
        var events = await _eventStore.LoadStreamAsync(streamId);

        if (events.Count == 0)
            return null;

        var cart = new ShoppingCart();
        cart.LoadFromHistory(events.Cast<object>().ToList());
        return cart;
    }

    public async Task SaveAsync(ShoppingCart aggregate)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        var streamId = $"cart-{aggregate.CartId}";
        var uncommittedEvents = aggregate.GetUncommittedEvents();

        if (uncommittedEvents.Count == 0)
            return;

        await _eventStore.AppendAsync(streamId, uncommittedEvents);
        aggregate.ClearUncommittedEvents();
    }
}

/// <summary>
/// CQRS Projection Writer for Cart events
/// Updates MongoDB read model collections from domain events
/// </summary>
public class CartProjectionWriter : ICartProjectionWriter
{
    private readonly IMongoDatabase _database;

    public CartProjectionWriter(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task HandleCartCreatedAsync(CartCreatedEvent @event)
    {
        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");

        var cartReadModel = new CartReadModel
        {
            CartId = @event.CartId.Value,
            GuestToken = @event.GuestToken.Value,
            Items = new(),
            CreatedAt = @event.OccurredAt,
            LastModifiedAt = @event.OccurredAt
        };

        await cartsCollection.InsertOneAsync(cartReadModel);
    }

    public async Task HandleCartItemAddedAsync(CartItemAddedEvent @event)
    {
        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");

        var filter = Builders<CartReadModel>.Filter.Eq(c => c.CartId, @event.CartId.Value);
        var update = Builders<CartReadModel>.Update
            .Push(c => c.Items, new CartItemView
            {
                ProductId = @event.ProductId.Value,
                Quantity = @event.Quantity.Value
            })
            .Set(c => c.LastModifiedAt, @event.OccurredAt);

        await cartsCollection.UpdateOneAsync(filter, update);
    }

    public async Task HandleCartItemQuantityUpdatedAsync(CartItemQuantityUpdatedEvent @event)
    {
        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");

        var filter = Builders<CartReadModel>.Filter.And(
            Builders<CartReadModel>.Filter.Eq(c => c.CartId, @event.CartId.Value),
            Builders<CartReadModel>.Filter.ElemMatch(c => c.Items, i => i.ProductId == @event.ProductId.Value)
        );

        var update = Builders<CartReadModel>.Update
            .Set("Items.$[item].Quantity", @event.NewQuantity.Value)
            .Set(c => c.LastModifiedAt, @event.OccurredAt);

        var arrayFilters = new List<ArrayFilterDefinition>
        {
            new BsonArrayFilterDefinition<BsonDocument>(
                MongoDB.Bson.BsonDocument.Parse($"{{ 'item.ProductId': '{@event.ProductId.Value}' }}"))
        };

        await cartsCollection.UpdateOneAsync(filter, update, new UpdateOptions { ArrayFilters = arrayFilters });
    }

    public async Task HandleCartItemRemovedAsync(CartItemRemovedEvent @event)
    {
        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");

        var filter = Builders<CartReadModel>.Filter.Eq(c => c.CartId, @event.CartId.Value);
        var update = Builders<CartReadModel>.Update
            .PullFilter(c => c.Items, i => i.ProductId == @event.ProductId.Value)
            .Set(c => c.LastModifiedAt, @event.OccurredAt);

        await cartsCollection.UpdateOneAsync(filter, update);
    }

    public async Task HandleCartClearedAsync(CartClearedEvent @event)
    {
        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");

        var filter = Builders<CartReadModel>.Filter.Eq(c => c.CartId, @event.CartId.Value);
        var update = Builders<CartReadModel>.Update
            .Set(c => c.Items, new List<CartItemView>())
            .Set(c => c.LastModifiedAt, @event.OccurredAt);

        await cartsCollection.UpdateOneAsync(filter, update);
    }
}
