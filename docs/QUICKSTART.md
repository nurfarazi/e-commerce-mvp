# Quick Start Guide - ProductCatalog Subsystem

## One-Time Setup (5 minutes)

### 1. Start Infrastructure
```bash
cd /Users/farazi/git/hackathon/e-commerce-mvp
docker-compose up -d
```

Verify:
- MongoDB: http://localhost:27017 (mongosh: `mongosh mongodb://admin:admin@localhost:27017`)
- RabbitMQ: http://localhost:15672 (user: guest, pass: guest)

### 2. Build Solution
```bash
dotnet build
```

### 3. Run Tests
```bash
dotnet test tests/
```

Expected: All tests pass (domain rules + handler logic)

## Running the System (in 4 separate terminals)

### Terminal 1: CommandApi (REST - accepts commands)
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandApi
dotnet run
# Listens on http://localhost:5000
```

### Terminal 2: QueryApi (REST - serves queries)
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.QueryApi
dotnet run
# Listens on http://localhost:5001
```

### Terminal 3: CommandHandler (worker - processes commands from RabbitMQ)
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler
dotnet run
# Logs: "Consuming commands from productcatalog.commands queue..."
```

### Terminal 4: EventHandler (worker - projects events to MongoDB)
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.EventHandler
dotnet run
# Logs: "Consuming events from productcatalog.projections queue..."
```

## Usage Examples

### Create a Product

```bash
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-001",
    "name": "MacBook Pro 16",
    "description": "High-performance laptop for professionals",
    "sku": "MB16-2024",
    "price": 2499.99,
    "currency": "USD"
  }'
```

Response (202 Accepted):
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "accepted",
  "productId": "PROD-001"
}
```

**What happens behind the scenes:**
1. CommandApi validates the request
2. Enqueues command to RabbitMQ `productcatalog.commands` queue
3. Returns 202 immediately (async)
4. CommandHandler dequeues and processes
5. Appends events to MongoDB EventStore
6. Publishes events to RabbitMQ fanout exchange
7. EventHandler receives and updates MongoDB read model

### Query the Product (immediately, or wait 1 sec for projection)

```bash
curl http://localhost:5001/api/products/PROD-001
```

Response (200 OK):
```json
{
  "productId": "PROD-001",
  "name": "MacBook Pro 16",
  "description": "High-performance laptop for professionals",
  "sku": "MB16-2024",
  "price": 2499.99,
  "currency": "USD",
  "isActive": true,
  "createdAt": "2026-01-24T12:34:56.123Z",
  "lastModifiedAt": "2026-01-24T12:34:56.123Z"
}
```

### List All Products

```bash
curl "http://localhost:5001/api/products?isActive=true&page=1&pageSize=10"
```

Response:
```json
{
  "data": [
    { "productId": "PROD-001", "name": "MacBook Pro 16", ... }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "total": 1,
    "totalPages": 1
  }
}
```

### Activate a Product

```bash
curl -X PUT http://localhost:5000/api/products/PROD-001/activate \
  -H "Content-Type: application/json"
```

Response (200 OK):
```json
{
  "message": "Product activated"
}
```

## Architecture Flow Diagram

```
┌─ CommandApi:5000 ─────────┐
│                            │
│  POST /products            │
│  (validates, enqueues)     │
│                            │
└────────────────────────────┘
          │
          ├─→ RabbitMQ productcatalog.commands queue
          │
          │  ┌─ CommandHandler ────────────────┐
          │  │                                  │
          │  │ • Dequeue command               │
          │  │ • Load aggregate (replay)       │
          │  │ • Execute behavior              │
          │  │ • Append events to EventStore   │
          │  │ • Publish to fanout exchange    │
          │  │                                  │
          │  └──────────────────────────────────┘
          │
          ├─→ RabbitMQ ProductCatalog.events (fanout)
          │
          │  ┌─ EventHandler ──────────────┐
          │  │                              │
          │  │ • Dequeue event             │
          │  │ • Update MongoDB projection │
          │  │ • Mark as processed         │
          │  │                              │
          │  └──────────────────────────────┘
          │
          └─→ MongoDB read model ready
                   │
                   │ ← QueryApi:5001 reads
                   │
                   ├─→ GET /api/products/{id}
                   │
                   └─→ HTTP 200 + ProductDto
```

## Key Files to Review

### Domain Logic
- [Product.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/Product.cs) - Aggregate root with business rules
- [Events.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/Events.cs) - Domain events

### Command Handlers
- [Commands.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/Commands.cs) - CreateProductCommand, ActivateProductCommand

### Event Projection
- [ProductProjectionWriter.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.Infrastructure/ProductProjectionWriter.cs) - Updates read models

### REST Controllers
- [CommandApi - ProductsController.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandApi/Controllers/ProductsController.cs) - Command endpoints
- [QueryApi - ProductsController.cs](src/ProductCatalog/ECommerceMvp.ProductCatalog.QueryApi/Controllers/ProductsController.cs) - Query endpoints

### Unit Tests
- [ProductTests.cs](tests/ECommerceMvp.ProductCatalog.Domain.Tests/ProductTests.cs) - Domain behavior tests
- [CommandHandlerTests.cs](tests/ECommerceMvp.ProductCatalog.Application.Tests/CommandHandlerTests.cs) - Handler tests

## Common Tasks

### Monitor Logs

All processes output structured logs with CorrelationId tracking. Example:
```
[12:34:56] [Info] [CorrelationId: 550e8400-e29b-41d4-a716-446655440000] Create product request: PROD-001
[12:34:56] [Debug] Creating product PROD-001
[12:34:56] [Debug] Product PROD-001 created successfully
[12:34:57] [Debug] Created product read model for PROD-001
```

### Check MongoDB

```bash
mongosh mongodb://admin:admin@localhost:27017/ecommerce

# View events
db.Events.find().pretty()

# View read model
db.Products.find().pretty()

# View idempotency markers
db.ProcessedCommands.find().pretty()
db.ProcessedEvents.find().pretty()
```

### Check RabbitMQ

Visit http://localhost:15672 → Queues tab → see:
- `productcatalog.commands` (commands from CommandApi)
- `productcatalog.projections` (events for EventHandler)

### Rebuild Single Project

```bash
dotnet build src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/
```

### Run Single Test File

```bash
dotnet test tests/ECommerceMvp.ProductCatalog.Domain.Tests/ProductTests.cs
```

## Troubleshooting

### "MongoDB Connection Failed"
- Check Docker: `docker ps | grep mongodb`
- If not running: `docker-compose up -d`
- Verify connection: `mongosh mongodb://admin:admin@localhost:27017`

### "RabbitMQ Connection Failed"
- Check Docker: `docker ps | grep rabbitmq`
- If not running: `docker-compose up -d`
- Check management UI: http://localhost:15672

### "ConcurrencyException on Create"
- Duplicate ProductId sent twice (idempotency working)
- Use different ProductId or clear database

### CommandHandler Not Processing
- Check logs for errors
- Verify RabbitMQ is running and queue exists
- Verify MongoDB event store is accessible

## Clean Up

### Stop Infrastructure
```bash
docker-compose down
```

### Remove Volumes (reset database)
```bash
docker-compose down -v
```

### Clean Build Artifacts
```bash
dotnet clean
rm -rf bin obj
```

## Next Steps (After Review)

Once you review and approve the ProductCatalog structure:

1. **Inventory Subsystem** - Same 4-process pattern
   - Aggregate: InventoryItem (tracks stock levels)
   - Commands: AddInventory, ReserveInventory, ReleaseReservation
   - Events: InventoryAddedEvent, InventoryReservedEvent, etc.

2. **Cart Subsystem**
   - Aggregate: ShoppingCart (items + quantities)
   - Commands: AddToCart, RemoveFromCart, ClearCart
   - Events: ItemAddedEvent, ItemRemovedEvent, etc.

3. **SAGA Orchestrator**
   - Coordinates PlaceOrderSaga
   - Subscribes to all subsystem events
   - Emits commands to drive workflow

---

**Happy developing!** 🚀
