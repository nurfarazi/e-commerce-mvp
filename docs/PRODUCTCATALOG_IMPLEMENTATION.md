# ProductCatalog Bounded Context Implementation

## Overview
Complete implementation of the ProductCatalog bounded context following Domain-Driven Design (DDD) principles with Event Sourcing and CQRS patterns.

## Architecture

### Domain Layer
The domain layer implements the core business logic with aggregate roots, value objects, and domain events.

#### Aggregate Root: Product
- **File**: `src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/Product.cs`
- **Identity**: String-based ProductId
- **State**: Name, Description, SKU, Price, IsActive

#### Value Objects
All value objects implement immutability and equality by value:

1. **ProductId**
   - Unique identifier for products
   - Non-empty string validation
   - Implicit conversion to/from string

2. **ProductName**
   - Constraints: Required, minimum 2 characters
   - Auto-trimmed on creation
   - Immutable after creation

3. **ProductDescription**
   - Optional field
   - Auto-trimmed on creation
   - Defaults to empty string

4. **Sku** (Stock Keeping Unit)
   - Constraints: Required, unique, immutable after creation (MVP rule)
   - Normalized to uppercase
   - Enforced at database level with unique index

5. **Price**
   - Constraints: Amount >= 0
   - Includes currency (default: USD)
   - Formatted as "Amount Currency" in ToString()

### Domain Behaviors (Methods)

All behaviors validate invariants and emit domain events:

#### CreateProduct
```csharp
public static Product Create(string id, string sku, string name, 
    decimal price, string? description = null)
```
- Creates new product instance
- Emits: `ProductCreatedEvent`
- Invariants checked: SKU unique, Name min length, Price >= 0

#### UpdateDetails
```csharp
public void UpdateDetails(string name, string? description = null)
```
- Updates product name and description
- Emits: `ProductDetailsUpdatedEvent`
- Only works on active products

#### ChangePrice
```csharp
public void ChangePrice(decimal newPrice, string currency = "USD")
```
- Changes product price
- Emits: `ProductPriceChangedEvent` (includes old and new prices)
- Only works on active products

#### Activate
```csharp
public void Activate()
```
- Activates product in catalog
- Emits: `ProductActivatedEvent`
- Enables selling and ordering

#### Deactivate
```csharp
public void Deactivate()
```
- Deactivates product from catalog
- Emits: `ProductDeactivatedEvent`
- Prevents selling and ordering

## Domain Events

All domain events inherit from `DomainEvent` base class with EventId, EventType, EventVersion, and OccurredAt metadata.

### ProductCreatedEvent
Emitted when a new product is created.
```
{
  productId: string,
  sku: string,
  name: string,
  price: decimal,
  currency: string (default: "USD"),
  description: string
}
```

### ProductDetailsUpdatedEvent
Emitted when product name or description is updated.
```
{
  productId: string,
  name: string,
  description: string
}
```

### ProductPriceChangedEvent
Emitted when product price is changed.
```
{
  productId: string,
  oldPrice: decimal,
  newPrice: decimal,
  oldCurrency: string,
  newCurrency: string
}
```

### ProductActivatedEvent
Emitted when product is activated.
```
{
  productId: string
}
```

### ProductDeactivatedEvent
Emitted when product is deactivated.
```
{
  productId: string
}
```

## Application Layer Commands

The application layer implements the CQRS command side using command handlers.

### CreateProductCommand
```csharp
{
  ProductId: string (required),
  Sku: string (required),
  Name: string (required),
  Price: decimal (required, >= 0),
  Currency: string (optional, default: "USD"),
  Description: string (optional)
}
```
**Handler**: `CreateProductCommandHandler`
- Validates all required fields
- Creates Product aggregate
- Saves to event store
- Publishes domain events

### UpdateProductDetailsCommand
```csharp
{
  ProductId: string (required),
  Name: string (required),
  Description: string (optional)
}
```
**Handler**: `UpdateProductDetailsCommandHandler`
- Validates ProductId and Name
- Loads product from event store
- Calls UpdateDetails() behavior
- Saves changes

### ChangeProductPriceCommand
```csharp
{
  ProductId: string (required),
  NewPrice: decimal (required, >= 0),
  Currency: string (optional, default: "USD")
}
```
**Handler**: `ChangeProductPriceCommandHandler`
- Validates ProductId and NewPrice
- Loads product from event store
- Calls ChangePrice() behavior
- Publishes PriceChangedEvent

### ActivateProductCommand
```csharp
{
  ProductId: string (required)
}
```
**Handler**: `ActivateProductCommandHandler`
- Loads product from event store
- Calls Activate() behavior
- Publishes ProductActivatedEvent

### DeactivateProductCommand
```csharp
{
  ProductId: string (required)
}
```
**Handler**: `DeactivateProductCommandHandler`
- Loads product from event store
- Calls Deactivate() behavior
- Publishes ProductDeactivatedEvent

## Application Layer Queries

The application layer implements the CQRS query side using query handlers and read models.

### CQRS Read Models (Projections)

#### ProductListView
Optimized for displaying product lists:
```csharp
{
  ProductId: string,
  Name: string,
  Price: decimal,
  Currency: string,
  IsActive: bool
}
```

#### ProductDetailView
Optimized for displaying product details:
```csharp
{
  ProductId: string,
  Sku: string,
  Name: string,
  Description: string,
  Price: decimal,
  Currency: string,
  IsActive: bool,
  CreatedAt: DateTimeOffset,
  LastModifiedAt: DateTimeOffset
}
```

### Query Operations

#### GetProductByIdQuery
Returns `ProductDetailView?` for a single product.

#### ListActiveProductsQuery
Returns paginated `IEnumerable<ProductListView>` of active products only.
- Parameters: Page (default: 1), PageSize (default: 20, max: 100)

#### ListAllProductsQuery
Returns paginated `IEnumerable<ProductListView>` of all products (active and inactive).
- Parameters: Page (default: 1), PageSize (default: 20, max: 100)

#### SearchProductsByNameQuery
Returns paginated `IEnumerable<ProductListView>` matching search term.
- Parameters: SearchTerm (required), OnlyActive (default: true), Page, PageSize

## Infrastructure Layer

### Event Store
- **Type**: MongoDB
- **Collection**: Events (from Shared.Infrastructure)
- **Purpose**: Immutable event log for event sourcing

### Read Model Database
- **Type**: MongoDB
- **Collection**: Products
- **Purpose**: Denormalized projections for queries

### ProjectionWriter
**File**: `src/ProductCatalog/ECommerceMvp.ProductCatalog.Infrastructure/ProductProjectionWriter.cs`

Implements `IProductProjectionWriter` interface with event handlers:

- `HandleProductCreatedAsync`: Creates new product read model
- `HandleProductDetailsUpdatedAsync`: Updates Name and Description
- `HandleProductPriceChangedAsync`: Updates Price and Currency
- `HandleProductActivatedAsync`: Sets IsActive to true
- `HandleProductDeactivatedAsync`: Sets IsActive to false

#### Database Indexes
- **ProductId**: Ascending (primary lookup)
- **IsActive**: Ascending (for ListActiveProductsQuery)
- **Sku**: Ascending + Unique (enforces SKU uniqueness)
- **Name**: Text index (for SearchProductsByNameQuery)
- **IsActive + CreatedAt**: Compound (for pagination)

### Read Model: ProductReadModel
MongoDB document structure:
```csharp
{
  _id: ObjectId,
  ProductId: string,
  Sku: string,
  Name: string,
  Description: string,
  Price: decimal,
  Currency: string,
  IsActive: bool,
  CreatedAt: DateTimeOffset,
  LastModifiedAt: DateTimeOffset
}
```

## Key Invariants

1. **SKU Uniqueness**: SKU is unique and immutable after creation (enforced via unique index)
2. **Price Non-Negative**: Price amount must be >= 0
3. **Name Required**: Product name is required and minimum 2 characters
4. **Active Products Only**: Only active products can be sold/added to orders
5. **State Transitions**: Only active products can have details or price updated

## Event Sourcing Flow

1. **Command Received**: CreateProductCommand
2. **Load Aggregate**: Retrieve Product from event store (if exists)
3. **Apply Behavior**: Call Product.Create() or other method
4. **Generate Events**: Domain events are appended to uncommitted events list
5. **Save Aggregate**: Events are persisted to MongoDB event store
6. **Publish Events**: Events are published to RabbitMQ message bus
7. **Project Events**: Event handlers update read models in MongoDB
8. **Query Response**: Queries read from optimized read model

## Error Handling

All command and query handlers implement consistent error handling:
- Input validation with descriptive error messages
- Try-catch blocks with structured logging
- Graceful degradation returning error responses
- Specific exception types for domain violations

## Testing

### Domain Tests
Located in: `tests/ECommerceMvp.ProductCatalog.Domain.Tests/ProductTests.cs`
- Aggregate root creation and state transitions
- Value object validation
- Invariant enforcement
- Event application

### Application Tests
Located in: `tests/ECommerceMvp.ProductCatalog.Application.Tests/CommandHandlerTests.cs`
- Command handler workflows
- Repository interactions
- Event publishing
- Error scenarios

## API Endpoints

The ProductCatalog bounded context exposes two APIs:

### Command API
- **Project**: `ECommerceMvp.ProductCatalog.CommandApi`
- **Port**: Configured in appsettings.json
- **Purpose**: Accept commands for product mutations

### Query API
- **Project**: `ECommerceMvp.ProductCatalog.QueryApi`
- **Port**: Configured in appsettings.json
- **Purpose**: Serve read model queries

## Deployment

### Background Workers
- **ProductCommandWorker**: Processes queued commands from RabbitMQ
- **ProductEventHandler**: Projects domain events to read models

### Configuration
All services configured via `appsettings.json`:
- MongoDB connection string
- RabbitMQ connection string
- Database names
- API ports

## Files Modified/Created

### Domain
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/Product.cs` ✅
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/Events.cs` ✅

### Application
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/Commands.cs` ✅
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/Queries.cs` ✅
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/EventHandlers.cs` ✅

### Infrastructure
- `src/ProductCatalog/ECommerceMvp.ProductCatalog.Infrastructure/ProductProjectionWriter.cs` ✅

## Compliance Checklist

- ✅ Aggregate Root: Product
- ✅ Value Objects: ProductId, ProductName, ProductDescription, Sku, Price
- ✅ Behaviors: CreateProduct, UpdateDetails, ChangePrice, Activate, Deactivate
- ✅ Invariants: SKU unique/immutable, Price >= 0, Name required (min 2), Active only
- ✅ Commands: Create, UpdateDetails, ChangePrice, Activate, Deactivate
- ✅ Domain Events: ProductCreated, ProductDetailsUpdated, ProductPriceChanged, ProductActivated, ProductDeactivated
- ✅ Read Models: ProductListView, ProductDetailView
- ✅ Query Handlers: GetById, ListActive, ListAll, Search
- ✅ Event Handlers: All event handlers implemented with full projection logic
- ✅ Tests: Domain and application test structure in place
