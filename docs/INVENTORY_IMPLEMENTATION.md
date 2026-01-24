# Inventory Subsystem Implementation Summary

## Overview
The Inventory Subsystem has been fully implemented following the same event-driven, CQRS architecture patterns as the ProductCatalog subsystem. It manages stock for products in the e-commerce MVP.

## Architecture

### Domain Model (Inventory.Domain)

**Aggregate Root: InventoryItem**
- Represents inventory for a single product identified by ProductId
- Manages stock quantity with validation

**Value Objects:**
- **ProductId**: Unique identifier for a product
- **Quantity**: Stock quantity (int >= 0, immutable)
- **AdjustmentReason**: Optional reason for manual stock adjustments

**Domain Events:**
- `StockItemCreatedEvent`: Fired when inventory is created for a product
- `StockSetEvent`: Fired when stock is manually set (admin operation)
- `StockDeductedForOrderEvent`: Fired when stock is deducted for an order (atomic per order)
- `StockDeductionRejectedEvent`: Fired when deduction fails due to insufficient inventory

**Key Behaviors:**
- `SetStock(newQty, reason?, changedBy?)`: Set stock to a new quantity (admin)
- `EnsureAvailable(requestedQty)`: Validate if quantity is available (read-only check)
- `DeductForOrder(orderId, qty)`: Atomically deduct stock for an order (idempotent per order)

### Application Layer (Inventory.Application)

**Commands & Handlers:**

1. **SetStockCommand** → `SetStockCommandHandler`
   - Creates inventory item if new, updates if existing
   - Publishes StockSetEvent
   - Validates quantity >= 0

2. **ValidateStockCommand** → `ValidateStockCommandHandler`
   - Queries multiple products for stock availability
   - Returns list of validation results
   - Non-mutating query-like operation

3. **DeductStockForOrderCommand** → `DeductStockForOrderCommandHandler`
   - Deducts stock for multiple items in an order
   - Atomic per order with idempotency protection
   - Rejects items with insufficient inventory
   - Publishes deduction events

**Queries & Handlers:**

1. **GetStockAvailabilityQuery** → Returns stock for a single product
2. **GetMultipleStockAvailabilityQuery** → Returns stock for multiple products

**Read Models:**
- **StockAvailabilityView**: `(productId, availableQuantity, inStockFlag, lastUpdatedAt)`
- **LowStockView**: `(productId, availableQuantity, lowStockThreshold, isLow, alertedAt)`

**Event Handlers:**
- `InventoryEventHandler`: Routes domain events to projection writers
- Projects events to read models for query performance

### Infrastructure Layer (Inventory.Infrastructure)

**InventoryRepository**
- Implements event sourcing pattern
- Loads/reconstructs InventoryItem from event stream
- Saves uncommitted events to event store
- Uses MongoDB for persistence
- Stream naming: `inventory-{productId}`

**InventoryProjectionWriter**
- Updates `StockAvailability` collection on stock changes
- Maintains `LowStock` collection for products below threshold (10 units)
- Ensures MongoDB indexes on ProductId and IsLow flag
- Handles upserts for efficient updates

### API Layers

**Inventory.CommandApi (Port 5002)**
- `/api/inventory/{productId}/set-stock` - POST: Set stock quantity
- `/api/inventory/validate-stock` - POST: Validate stock availability
- `/api/inventory/deduct-for-order` - POST: Deduct stock for order

**Inventory.QueryApi (Port 5003)**
- `/api/inventory/{productId}/availability` - GET: Get stock for product
- `/api/inventory/availability/batch` - POST: Get stock for multiple products
- `/api/inventory/{productId}/in-stock` - GET: Check if in stock
- `/api/inventory/low-stock` - GET: List all low-stock products

### Workers

**Inventory.CommandHandler**
- Background service listening to `inventory.commands` RabbitMQ queue
- Routes command messages to appropriate handlers
- Deserialization of command envelopes
- Acknowledgment on successful processing

**Inventory.EventHandler**
- Background service listening to `inventory.projections` RabbitMQ queue
- Binds to `Inventory.events` fanout exchange
- Routes events to projection writer
- Updates read models for queries

## Project Structure

```
src/Inventory/
├── ECommerceMvp.Inventory.Domain/
│   ├── InventoryItem.cs        (Aggregate root, value objects)
│   ├── Events.cs                (Domain events)
│   └── ECommerceMvp.Inventory.Domain.csproj
├── ECommerceMvp.Inventory.Application/
│   ├── Commands.cs              (SetStock, ValidateStock, DeductStockForOrder)
│   ├── Queries.cs               (Read models, query handlers)
│   ├── EventHandlers.cs         (Event routing to projections)
│   └── ECommerceMvp.Inventory.Application.csproj
├── ECommerceMvp.Inventory.Infrastructure/
│   ├── InventoryRepository.cs   (Event sourcing)
│   ├── InventoryProjectionWriter.cs (MongoDB projections)
│   └── ECommerceMvp.Inventory.Infrastructure.csproj
├── ECommerceMvp.Inventory.CommandApi/
│   ├── Controllers/InventoryController.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── ECommerceMvp.Inventory.CommandApi.csproj
├── ECommerceMvp.Inventory.CommandHandler/
│   ├── InventoryCommandWorker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── ECommerceMvp.Inventory.CommandHandler.csproj
├── ECommerceMvp.Inventory.EventHandler/
│   ├── InventoryEventWorker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── ECommerceMvp.Inventory.EventHandler.csproj
└── ECommerceMvp.Inventory.QueryApi/
    ├── Controllers/StockQueryController.cs
    ├── Program.cs
    ├── appsettings.json
    └── ECommerceMvp.Inventory.QueryApi.csproj
```

## Key Invariants & Validation

1. **Stock Quantity Never Negative**: Enforced at value object level (Quantity)
2. **Atomic Deductions**: Each deduction is per-order, atomic operation
3. **Idempotency**: Same (orderId, productId) deduction cannot double-apply
4. **Product Must Be Active**: Validated at command boundary by Ordering orchestration
5. **Low Stock Threshold**: Products with qty < 10 tracked in LowStockView

## Integration Points

**Ordering Subsystem:**
- Validates product inventory before checkout via `ValidateStockCommand`
- Deducts stock on order confirmation via `DeductStockForOrderCommand`
- Receives rejection events if insufficient inventory

**ProductCatalog Subsystem:**
- Inventory doesn't own "active" state
- Relies on Catalog to indicate which products are active
- Can ignore inactive products in stock checks

## Configuration

**MongoDB Collections:**
- `EventStore`: Stores all domain events
- `Idempotency`: Tracks processed command IDs
- `StockAvailability`: Read model for quick stock queries
- `LowStock`: Read model for inventory alerts

**RabbitMQ Queues/Exchanges:**
- `inventory.commands`: Command queue (durable)
- `Inventory.events`: Event fanout exchange
- `inventory.projections`: Projection queue (binds to events exchange)

## Error Handling

- **InsufficientInventoryException**: Thrown when deduction fails, recorded as `StockDeductionRejectedEvent`
- **ArgumentException**: Thrown for invalid inputs (empty productId, negative quantity, etc.)
- **InvalidOperationException**: Thrown for invalid state transitions
- All exceptions logged with correlation IDs for tracing

## Future Enhancements (Not in MVP)

1. **Stock Restore**: `RestoreStockForOrderCommand` when order is cancelled
2. **Inventory Reservations**: Temporary holds for in-progress orders
3. **Multi-warehouse Support**: Distributed inventory across warehouses
4. **Stock Transfer**: Movement between warehouses
5. **Audit Trail**: Detailed change history with user tracking
6. **Batch Operations**: Bulk inventory updates
7. **Webhook Notifications**: External system alerts on low stock

## Testing Recommendations

1. **Domain Tests**: 
   - Quantity validation
   - Deduction atomicity
   - Event application

2. **Application Tests**:
   - Command validation
   - Handler logic
   - Event publishing

3. **Integration Tests**:
   - Event sourcing round-trip
   - MongoDB persistence
   - RabbitMQ message routing

4. **API Tests**:
   - HTTP contract verification
   - Request/response validation
   - Error scenarios

## Running the Subsystem

### Prerequisites
- MongoDB running on localhost:27017 (user: admin, pass: admin)
- RabbitMQ running on localhost:5672 (user: guest, pass: guest)

### Build
```bash
dotnet build
```

### Run Individual Services
```bash
# CommandApi (port 5002)
cd src/Inventory/ECommerceMvp.Inventory.CommandApi
dotnet run

# QueryApi (port 5003)
cd src/Inventory/ECommerceMvp.Inventory.QueryApi
dotnet run

# CommandHandler (background worker)
cd src/Inventory/ECommerceMvp.Inventory.CommandHandler
dotnet run

# EventHandler (background worker)
cd src/Inventory/ECommerceMvp.Inventory.EventHandler
dotnet run
```

## API Examples

### Set Stock (Admin)
```http
POST /api/inventory/PROD-001/set-stock
Content-Type: application/json

{
  "newQuantity": 100,
  "reason": "Initial stock setup",
  "changedBy": "admin@example.com"
}
```

### Validate Stock
```http
POST /api/inventory/validate-stock
Content-Type: application/json

{
  "items": [
    { "productId": "PROD-001", "requestedQuantity": 5 },
    { "productId": "PROD-002", "requestedQuantity": 3 }
  ]
}
```

### Deduct Stock for Order
```http
POST /api/inventory/deduct-for-order
Content-Type: application/json

{
  "orderId": "ORD-12345",
  "items": [
    { "productId": "PROD-001", "quantity": 2 },
    { "productId": "PROD-002", "quantity": 1 }
  ]
}
```

### Query Stock Availability
```http
GET /api/inventory/PROD-001/availability
```

### Check If In Stock
```http
GET /api/inventory/PROD-001/in-stock
```

### Get Low Stock Products
```http
GET /api/inventory/low-stock
```
