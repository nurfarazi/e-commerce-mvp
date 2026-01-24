# Inventory Subsystem - Quick Start Guide

## What Was Implemented

The Inventory Subsystem manages product stock in the e-commerce MVP following the same event-driven CQRS architecture as ProductCatalog.

### Core Components

**8 Projects Created:**
1. ✅ **Inventory.Domain** - Aggregate root, value objects, domain events
2. ✅ **Inventory.Application** - Commands, queries, event handlers  
3. ✅ **Inventory.Infrastructure** - Repository, projections, MongoDB
4. ✅ **Inventory.CommandApi** - HTTP API for commands (Port 5002)
5. ✅ **Inventory.CommandHandler** - RabbitMQ command worker
6. ✅ **Inventory.EventHandler** - RabbitMQ event worker  
7. ✅ **Inventory.QueryApi** - HTTP API for queries (Port 5003)
8. ✅ **Solution file updated** - All projects referenced in ECommerceMvp.sln

---

## Architecture Patterns

### Aggregate Root: InventoryItem
Manages stock for a single product with these behaviors:

```csharp
// Create new inventory
InventoryItem.Create(productId, initialQuantity)

// Set stock (admin)
item.SetStock(newQuantity, reason?, changedBy?)

// Validate availability (read-only check)
bool hasStock = item.EnsureAvailable(requestedQuantity)

// Deduct for order (atomic + idempotent)
item.DeductForOrder(orderId, quantityToDeduct)
```

### Domain Events
- `StockItemCreatedEvent` - Initial creation
- `StockSetEvent` - Manual adjustment
- `StockDeductedForOrderEvent` - Order fulfillment
- `StockDeductionRejectedEvent` - Insufficient inventory

### Value Objects
- `ProductId` - Product identifier
- `Quantity` - Stock amount (>= 0)
- `AdjustmentReason` - Optional reason string

---

## API Endpoints

### Command API (Port 5002)

**Set Stock**
```bash
POST /api/inventory/{productId}/set-stock
Body: { "newQuantity": 100, "reason": "Initial stock", "changedBy": "admin" }
```

**Validate Stock**
```bash
POST /api/inventory/validate-stock
Body: { "items": [{ "productId": "PROD-001", "requestedQuantity": 5 }] }
```

**Deduct Stock for Order**
```bash
POST /api/inventory/deduct-for-order
Body: { "orderId": "ORD-123", "items": [{ "productId": "PROD-001", "quantity": 2 }] }
```

### Query API (Port 5003)

**Get Stock for Product**
```bash
GET /api/inventory/{productId}/availability
```

**Check In Stock**
```bash
GET /api/inventory/{productId}/in-stock
```

**Get Multiple Products**
```bash
POST /api/inventory/availability/batch
Body: { "productIds": ["PROD-001", "PROD-002"] }
```

**Get Low Stock Products**
```bash
GET /api/inventory/low-stock
```

---

## Data Flow

### Write Flow (Commands)
1. **Client** → HTTP POST to **CommandApi**
2. **CommandApi** → Dispatches command to handler
3. **Handler** → Creates/modifies **InventoryItem** aggregate
4. **Aggregate** → Generates domain events
5. **Repository** → Saves events to **MongoDB EventStore**
6. **Publisher** → Publishes events to **RabbitMQ**
7. **CommandHandler Worker** → Listens to command queue (if using command enqueuing)

### Projection Flow (Read Models)
1. **EventHandler Worker** → Listens to `inventory.projections` queue
2. **Events** → Deserialized from RabbitMQ
3. **ProjectionWriter** → Updates MongoDB read models
4. **StockAvailability** → Updated with current stock
5. **LowStock** → Updated if below threshold (10)

### Query Flow (Reads)
1. **Client** → HTTP GET to **QueryApi**
2. **QueryApi** → Queries MongoDB read models
3. **MongoDB** → Returns optimized view
4. **QueryApi** → Returns to client

---

## Database Schema

### MongoDB Collections

**EventStore**
```
{
  _id: ObjectId,
  StreamId: "inventory-PROD-001",
  Events: [
    { eventType: "StockItemCreated", payload: {...} },
    { eventType: "StockSet", payload: {...} },
    ...
  ],
  Version: 3,
  Timestamp: ISODate
}
```

**StockAvailability (Read Model)**
```
{
  _id: ObjectId,
  ProductId: "PROD-001",
  AvailableQuantity: 85,
  InStockFlag: true,
  LastUpdatedAt: ISODate
}
```

**LowStock (Read Model)**
```
{
  _id: ObjectId,
  ProductId: "PROD-002",
  AvailableQuantity: 5,
  LowStockThreshold: 10,
  IsLow: true,
  AlertedAt: ISODate
}
```

### RabbitMQ Topology

**Queues:**
- `inventory.commands` - Commands for processing
- `inventory.projections` - Events for read model updates (binds to Inventory.events)

**Exchanges:**
- `Inventory.events` - Fanout exchange for event distribution

---

## Configuration

### MongoDB
- **Connection**: `mongodb://admin:admin@localhost:27017`
- **Database**: `ecommerce`
- **Username**: admin
- **Password**: admin

### RabbitMQ
- **Host**: localhost
- **Port**: 5672
- **Username**: guest
- **Password**: guest

### Application Settings (appsettings.json)
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://admin:admin@localhost:27017",
    "Database": "ecommerce"
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

---

## Key Invariants

1. **Stock Never Negative** - Enforced at Quantity value object
2. **Atomic Deductions** - All or nothing per order
3. **Idempotent** - Same orderId+productId won't double-deduct  
4. **Active Check** - Product must be active in Catalog
5. **Low Stock Alert** - Products < 10 units tracked
6. **Version Control** - Event sourcing ensures audit trail

---

## Integration with Other Subsystems

### ProductCatalog
- Inventory doesn't own "active" state
- Catalog owns active/inactive flags
- Integration happens at Ordering layer

### Ordering Subsystem (Future)
- **Validation**: `ValidateStockCommand` before checkout
- **Deduction**: `DeductStockForOrderCommand` on confirmation
- **Rejection Events**: Listen to `StockDeductionRejected`

---

## Building & Running

### Build Solution
```bash
dotnet build
```

### Run Individual Services

**CommandApi**
```bash
cd src/Inventory/ECommerceMvp.Inventory.CommandApi
dotnet run  # Runs on http://localhost:5002
```

**QueryApi**
```bash
cd src/Inventory/ECommerceMvp.Inventory.QueryApi
dotnet run  # Runs on http://localhost:5003
```

**CommandHandler** (Background Worker)
```bash
cd src/Inventory/ECommerceMvp.Inventory.CommandHandler
dotnet run
```

**EventHandler** (Background Worker)
```bash
cd src/Inventory/ECommerceMvp.Inventory.EventHandler
dotnet run
```

---

## Testing Workflow

### 1. Initialize Stock
```bash
curl -X POST http://localhost:5002/api/inventory/PROD-001/set-stock \
  -H "Content-Type: application/json" \
  -d '{"newQuantity": 100}'
```

### 2. Check Availability
```bash
curl http://localhost:5003/api/inventory/PROD-001/availability
# Returns: { availableQuantity: 100, inStockFlag: true, ... }
```

### 3. Validate Requirement
```bash
curl -X POST http://localhost:5002/api/inventory/validate-stock \
  -H "Content-Type: application/json" \
  -d '{"items": [{"productId": "PROD-001", "requestedQuantity": 30}]}'
# Returns: { success: true, results: [{ isAvailable: true }] }
```

### 4. Deduct for Order
```bash
curl -X POST http://localhost:5002/api/inventory/deduct-for-order \
  -H "Content-Type: application/json" \
  -d '{"orderId": "ORD-001", "items": [{"productId": "PROD-001", "quantity": 30}]}'
# Returns: { success: true, results: [{ quantityDeducted: 30, remainingQuantity: 70 }] }
```

### 5. Verify Updated Stock
```bash
curl http://localhost:5003/api/inventory/PROD-001/availability
# Returns: { availableQuantity: 70, inStockFlag: true, ... }
```

### 6. Check Low Stock
```bash
curl http://localhost:5003/api/inventory/low-stock
# Returns: [] (empty if all products > 10 units)
```

---

## File Organization

```
src/Inventory/
├── Domain/
│   ├── InventoryItem.cs      ← Aggregate root + value objects
│   ├── Events.cs             ← Domain events
│   └── .csproj
├── Application/
│   ├── Commands.cs           ← SetStock, ValidateStock, DeductForOrder
│   ├── Queries.cs            ← Read models + query handlers
│   ├── EventHandlers.cs      ← Event routing
│   └── .csproj
├── Infrastructure/
│   ├── InventoryRepository.cs      ← Event sourcing
│   ├── InventoryProjectionWriter.cs ← MongoDB projections
│   └── .csproj
├── CommandApi/
│   ├── Controllers/InventoryController.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── .csproj
├── CommandHandler/
│   ├── InventoryCommandWorker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── .csproj
├── EventHandler/
│   ├── InventoryEventWorker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── .csproj
└── QueryApi/
    ├── Controllers/StockQueryController.cs
    ├── Program.cs
    ├── appsettings.json
    └── .csproj
```

---

## Common Issues & Solutions

**Issue**: Port already in use
- **Solution**: Change port in Program.cs or kill existing process

**Issue**: MongoDB connection failed
- **Solution**: Ensure MongoDB is running and credentials match appsettings.json

**Issue**: RabbitMQ connection failed
- **Solution**: Ensure RabbitMQ service is running on port 5672

**Issue**: Events not being projected
- **Solution**: Check EventHandler is running and RabbitMQ queues exist

**Issue**: Insufficient inventory error
- **Solution**: Set higher stock quantity or reduce deduction amount

---

## Documentation References

- [Full Implementation Details](./INVENTORY_IMPLEMENTATION.md)
- [Complete API Reference](./INVENTORY_API_REFERENCE.md)
- [ProductCatalog Reference](./PRODUCTCATALOG_IMPLEMENTATION.md) (similar pattern)

---

## Next Steps

1. ✅ Core Inventory implemented
2. ⏳ Integration with Ordering subsystem
3. ⏳ Stock restoration for cancelled orders
4. ⏳ Inventory reservations for in-progress orders
5. ⏳ Multi-warehouse support
6. ⏳ Comprehensive unit/integration tests
7. ⏳ Docker containerization
