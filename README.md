# E-Commerce MVP - ProductCatalog Subsystem Implementation

This is a working proof-of-concept implementation of the **ProductCatalog Subsystem** for a distributed e-commerce system using DDD, CQRS, Clean Architecture, and event sourcing.

## Project Structure

```
e-commerce-mvp/
├── src/
│   ├── Shared/                                    # Shared abstractions and infrastructure
│   │   ├── ECommerceMvp.Shared.Domain/           # Core DDD interfaces (IAggregateRoot, IDomainEvent, etc.)
│   │   ├── ECommerceMvp.Shared.Application/      # CQRS interfaces (ICommand, IQuery, IRepository, etc.)
│   │   └── ECommerceMvp.Shared.Infrastructure/   # Implementations (MongoDB EventStore, RabbitMQ Publisher, Idempotency)
│   │
│   └── ProductCatalog/                           # ProductCatalog Bounded Context (independent subsystem)
│       ├── ECommerceMvp.ProductCatalog.Domain/   # Domain layer (Product aggregate, events, value objects)
│       ├── ECommerceMvp.ProductCatalog.Application/  # Application layer (commands, command handlers, queries)
│       ├── ECommerceMvp.ProductCatalog.Infrastructure/  # Repositories, projection writers
│       │
│       ├── ECommerceMvp.ProductCatalog.CommandApi/    # Process 1: REST API for commands (Port 5000)
│       ├── ECommerceMvp.ProductCatalog.CommandHandler/ # Process 2: RabbitMQ worker consuming commands
│       ├── ECommerceMvp.ProductCatalog.EventHandler/   # Process 3: RabbitMQ worker consuming events for projections
│       └── ECommerceMvp.ProductCatalog.QueryApi/      # Process 4: REST API for queries (Port 5001)
│
├── tests/
│   ├── ECommerceMvp.ProductCatalog.Domain.Tests/      # Domain logic unit tests
│   └── ECommerceMvp.ProductCatalog.Application.Tests/ # Application & handler unit tests
│
├── docker-compose.yml                           # MongoDB + RabbitMQ containers
└── ECommerceMvp.sln                             # Visual Studio solution
```

## Technology Stack

- **Language**: C# (.NET 8)
- **Database**: MongoDB (event store, projections, idempotency, saga state)
- **Messaging**: RabbitMQ (command queues, event fanout exchange)
- **Web**: ASP.NET Core
- **Logging**: Serilog (structured logging)
- **Testing**: xUnit, Moq
- **DI**: Microsoft.Extensions.DependencyInjection

## Architecture Overview

### Four Deployable Processes Per Subsystem

Each subsystem (bounded context) consists of 4 independently deployable processes:

#### 1. **CommandApi Process** (REST Ingress)
- **Port**: 5000
- **Responsibility**: Accept HTTP POST commands, validate request shape, enqueue to RabbitMQ
- **Does NOT**: Load aggregates, execute business logic, or write domain state
- **Endpoints**:
  - `POST /api/products` - Create product
  - `PUT /api/products/{id}/activate` - Activate product
- **Response**: HTTP 202 Accepted (async acknowledgment pattern)

#### 2. **CommandHandler Process** (Write Model Executor)
- **Type**: Background worker (no HTTP port)
- **Responsibility**: Consume commands from RabbitMQ, execute domain logic, persist events
- **Does NOT**: Query read models, serve REST endpoints
- **Flow**:
  1. Dequeue command from `productcatalog.commands` queue
  2. Check de-duplication (idempotency key)
  3. Load aggregate from event store (replay events)
  4. Execute aggregate behavior (enforces invariants)
  5. Append new events to event store (optimistic concurrency)
  6. Publish committed events to `ProductCatalog.events` fanout exchange
  7. Record processed command for idempotency

#### 3. **EventHandler Process** (Projection Writer)
- **Type**: Background worker (no HTTP port)
- **Responsibility**: Consume events from RabbitMQ, update denormalized read models
- **Does NOT**: Modify aggregates, accept commands
- **Flow**:
  1. Subscribe to `ProductCatalog.events` fanout exchange
  2. Receive events via `productcatalog.projections` queue
  3. Check idempotency (event already processed?)
  4. Update MongoDB `Products` read collection
  5. Record processed event for idempotency
  6. Acknowledge message to RabbitMQ

#### 4. **QueryApi Process** (Read Model Service)
- **Port**: 5001
- **Responsibility**: Serve read-only queries from MongoDB projections
- **Does NOT**: Accept commands, modify state, consume events
- **Endpoints**:
  - `GET /api/products/{id}` - Get product by ID
  - `GET /api/products?isActive=true&page=1&pageSize=20` - List products with filtering/pagination
- **Response**: HTTP 200 OK with product DTOs

### Communication Patterns

```
┌─────────────────────────────────────────────────────────────────┐
│ COMMAND FLOW (Writes)                                           │
└─────────────────────────────────────────────────────────────────┘

REST Client
    ↓ POST /api/products
    ↓
CommandApi (validates, generates metadata)
    ↓ enqueue to RabbitMQ
    ↓
productcatalog.commands queue
    ↓ consume
    ↓
CommandHandler (executes domain logic)
    ├→ load aggregate (replay events from event store)
    ├→ enforce business invariants
    ├→ append new events
    └→ publish to ProductCatalog.events exchange
    ↓
    
┌─────────────────────────────────────────────────────────────────┐
│ EVENT FLOW (Read Model Projections)                             │
└─────────────────────────────────────────────────────────────────┘

ProductCatalog.events (fanout exchange)
    ↓ broadcast to bound queues
    ↓
productcatalog.projections queue
    ↓ consume
    ↓
EventHandler (projects to read model)
    ├→ check idempotency
    ├→ update MongoDB Products collection
    └→ mark event as processed
    ↓
MongoDB read collections ready for queries

┌─────────────────────────────────────────────────────────────────┐
│ QUERY FLOW (Reads)                                              │
└─────────────────────────────────────────────────────────────────┘

REST Client
    ↓ GET /api/products/{id}
    ↓
QueryApi (reads from MongoDB only)
    ├→ apply authorization/filters
    ├→ fetch from read collection
    └→ return product DTO
    ↓
HTTP 200 OK
```

## Domain Model

### Aggregate Root: Product

**State**:
- `Id` (string, primary key)
- `Name` (string)
- `Description` (string)
- `Sku` (value object)
- `Price` (value object)
- `IsActive` (bool)

**Behaviors**:
- `Create(id, name, description, sku, price)` → ProductCreatedEvent
- `Update(name, description, price)` → ProductUpdatedEvent (only if active)
- `Activate()` → ProductActivatedEvent (only if inactive)
- `Deactivate()` → ProductDeactivatedEvent (only if active)

### Value Objects

- **Sku**: Immutable, equality by value, validates non-empty
- **Price**: Immutable, includes amount + currency, validates non-negative

### Domain Events

- `ProductCreatedEvent`: Contains ProductId, Name, Description, Sku, Price, Currency
- `ProductUpdatedEvent`: Contains ProductId, Name, Description, Price, Currency
- `ProductActivatedEvent`: Contains ProductId
- `ProductDeactivatedEvent`: Contains ProductId

## Data Storage

### MongoDB Collections

#### Event Store
```
Collection: Events
├── StreamId: "product-{ProductId}"      # Partition key
├── Version: 1, 2, 3, ...                 # Sequence within stream
├── EventId: UUID                          # For idempotency
├── EventType: "ProductCreatedEvent"
├── Payload: { ...event data... }
├── CorrelationId: UUID                    # Links workflow
├── CausationId: UUID (optional)
├── TenantId: string (optional)
└── CreatedAt: DateTimeOffset

Indexes:
  - (StreamId, Version) - Replay events
  - EventId - Idempotency
  - CorrelationId - Audit trail
```

#### Stream Metadata
```
Collection: StreamMetadata
├── StreamId: "product-{ProductId}"
├── Version: 1, 2, 3, ...                 # Current version
├── TenantId: string (optional)
├── CreatedAt: DateTimeOffset
└── LastModifiedAt: DateTimeOffset
```

#### Read Model (Projection)
```
Collection: Products (for queries)
├── ProductId: string (unique)
├── Name: string
├── Description: string
├── Sku: string
├── Price: decimal
├── Currency: string
├── IsActive: bool
├── CreatedAt: DateTimeOffset
└── LastModifiedAt: DateTimeOffset

Indexes:
  - ProductId - Get by ID
  - IsActive - Filter by status
  - Sku - Lookup by SKU
```

#### Processed Commands (Idempotency)
```
Collection: ProcessedCommands
├── CommandId: string (unique)
├── Result: { ...response... }
├── ProcessedAt: DateTimeOffset
└── ExpiresAt: DateTimeOffset (TTL: 24 hours)
```

#### Processed Events (Idempotency)
```
Collection: ProcessedEvents
├── EventId: string (unique)
├── HandlerName: string
├── ProcessedAt: DateTimeOffset
└── ExpiresAt: DateTimeOffset (TTL: 7 days)
```

## Idempotency Strategy

### Command Deduplication
- Each command has a unique `CommandId` (UUID)
- Before execution: check `ProcessedCommands` collection
- If found: return cached response
- If not found: execute and store result with 24-hour TTL

### Event Idempotency
- Each event has unique `EventId` (UUID)
- Projection handlers check `ProcessedEvents` collection
- If already processed: skip (don't duplicate in read model)
- Implemented via MongoDB upsert + idempotency marker

## Concurrency Strategy

**Optimistic Locking**:
- Aggregate maintains `Version` number
- On save: include `expectedVersion` in append command
- Event Store checks: `actual version == expectedVersion`?
- If mismatch: throw `ConcurrencyException` (retry with backoff)
- No pessimistic locks; conflicts resolved via retry

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- Git

### Setup

1. **Clone repository**:
   ```bash
   cd /Users/farazi/git/hackathon/e-commerce-mvp
   ```

2. **Start infrastructure** (MongoDB + RabbitMQ):
   ```bash
   docker-compose up -d
   ```

3. **Verify connectivity**:
   - MongoDB: `mongosh mongodb://admin:admin@localhost:27017`
   - RabbitMQ Management: http://localhost:15672 (guest/guest)

### Running Locally

#### Build Solution
```bash
dotnet build
```

#### Run Unit Tests
```bash
dotnet test tests/ECommerceMvp.ProductCatalog.Domain.Tests/
dotnet test tests/ECommerceMvp.ProductCatalog.Application.Tests/
```

#### Start Each Process (in separate terminals)

**CommandApi** (REST, accepts commands):
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandApi
dotnet run
# Listens on: http://localhost:5000
```

**QueryApi** (REST, serves queries):
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.QueryApi
dotnet run
# Listens on: http://localhost:5001
```

**CommandHandler** (Background worker):
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler
dotnet run
# Consumes from: productcatalog.commands queue
```

**EventHandler** (Background worker):
```bash
cd src/ProductCatalog/ECommerceMvp.ProductCatalog.EventHandler
dotnet run
# Consumes from: productcatalog.projections queue
```

### Testing the System

#### 1. Create a Product (Command)

```bash
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-001",
    "name": "Laptop",
    "description": "High-performance laptop",
    "sku": "SKU-12345",
    "price": 999.99,
    "currency": "USD"
  }'
```

**Response** (async acknowledgment):
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "accepted",
  "productId": "PROD-001"
}
```

#### 2. Wait for Projection (Event Handler processes)

Wait 1–2 seconds for:
- CommandHandler to process command from queue
- EventHandler to update MongoDB projection

#### 3. Query Product (Read Model)

```bash
curl http://localhost:5001/api/products/PROD-001
```

**Response**:
```json
{
  "productId": "PROD-001",
  "name": "Laptop",
  "description": "High-performance laptop",
  "sku": "SKU-12345",
  "price": 999.99,
  "currency": "USD",
  "isActive": true,
  "createdAt": "2026-01-24T12:00:00Z",
  "lastModifiedAt": "2026-01-24T12:00:00Z"
}
```

#### 4. List Products

```bash
curl "http://localhost:5001/api/products?isActive=true&page=1&pageSize=20"
```

**Response**:
```json
{
  "data": [
    { "productId": "PROD-001", ... }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "total": 1,
    "totalPages": 1
  }
}
```

#### 5. Activate Product

```bash
curl -X PUT http://localhost:5000/api/products/PROD-001/activate
```

**Response**:
```json
{
  "message": "Product activated"
}
```

## Key Design Decisions

### 1. Async Commands (202 Accepted)
Commands return immediately without waiting for completion. Clients poll or subscribe to events for results.

### 2. No Direct Process-to-Process Calls
Subsystems communicate exclusively via RabbitMQ (asynchronous). No REST calls between services.

### 3. Event-Driven Consistency
Read models are **eventually consistent** (milliseconds lag). Strong consistency available via event store direct queries if needed.

### 4. Command Deduplication Key (CommandId)
Prevents duplicate processing if request retried due to network failures.

### 5. Idempotent Projections
Upsert operations + idempotency markers ensure duplicate events don't duplicate projections.

### 6. Separate Read & Write Models
Write model (event store) optimized for correctness. Read model (MongoDB collections) optimized for queries.

## Extension Points for Other Subsystems

Each new subsystem (Inventory, Payments, Shipping, etc.) follows the same pattern:

1. **Define domain** (Aggregate Root, Events, Value Objects)
2. **Define application layer** (Commands, Queries, Handlers)
3. **Implement repositories & projections**
4. **Deploy 4 processes** (CommandApi, CommandHandler, EventHandler, QueryApi)
5. **Subscribe to other subsystems' events** (via fanout exchange)

## Cross-Subsystem SAGA Example (Future)

Once multiple subsystems exist, a **PlaceOrderSaga** will orchestrate:

```
Client places order
  ↓
Orders.CommandApi receives CreateOrderCommand
  ↓
Orders.CommandHandler executes → publishes OrderPlacedEvent
  ↓
SagaOrchestrator receives OrderPlacedEvent
  ├→ publishes ReserveInventoryCommand to Inventory
  ↓
Inventory.CommandHandler executes → publishes InventoryReservedEvent
  ├→ or InventoryReservationFailedEvent
  ↓
SagaOrchestrator receives event
  ├→ if success: publish ProcessPaymentCommand to Payments
  ├→ if failure: publish UndoReserveInventoryCommand
  ...
  ↓
Eventually: Order status = Completed or Failed
```

## Observability

### Structured Logging

All logs include:
- `[CorrelationId]` - Trace entire workflow
- `[CausationId]` - Track event causality
- `[TenantId]` - Tenant isolation (for multi-tenancy)
- Timestamp, log level, service name, message

Example:
```
[2026-01-24 12:00:15] [Info] [CorrelationId: 550e8400-e29b-41d4-a716-446655440000] [TenantId: TENANT-001] Create product request: PROD-001
```

### Metrics (Ready for Integration)

- `command_received_total` - Commands per type
- `command_duration_seconds` - Handler latency
- `command_failures_total` - Failed commands
- `events_published_total` - Events per type
- `eventstore_append_duration_seconds` - Persistence latency
- `projection_lag_seconds` - Read model staleness

### Tracing

All processes generate structured logs with CorrelationId and CausationId propagation ready for centralized tracing (e.g., ELK, Jaeger).

## Testing Strategy

### Domain Tests (ProductTests.cs)

- Aggregate creation with valid/invalid data
- Business rule enforcement (no update when inactive)
- Event generation
- Value object equality

### Application Tests (CommandHandlerTests.cs)

- Command handler success scenarios
- Command validation
- Repository interaction
- Event publishing

### Integration Tests (Future)

- MongoDB event store + replay
- RabbitMQ publish/consume
- Full command → event → projection flow
- Concurrency conflict handling

## Known Limitations / MVP Scope

1. **No Cross-Subsystem Transactions**: Sagas handle multi-subsystem workflows (choreography, not ACID)
2. **No Event Snapshots**: Aggregate replays all events (optimization for later)
3. **Minimal Error Recovery**: Poison message queues not fully implemented
4. **No Caching**: QueryAPI reads fresh from MongoDB each time
5. **Single Tenant**: TenantId passed but not enforced at DB layer (row-level security)
6. **No Authentication/Authorization**: REST endpoints not secured

## Next Steps

After review, the remaining subsystems to implement:

1. **Inventory** (same 4-process pattern)
2. **Cart** (same 4-process pattern)
3. **Checkout** (aggregates Cart + Products)
4. **Payment** (external payment gateway integration)
5. **OrderManagement** (orchestrates fulfillment)
6. **User** (guest identity)
7. **SAGA Orchestrator** (PlaceOrderSaga, etc.)

---

## Summary

This ProductCatalog implementation demonstrates:
✅ DDD (Aggregates, Value Objects, Domain Events)
✅ CQRS (Separate write/read models)
✅ Clean Architecture (layered dependencies)
✅ Event Sourcing (all state changes via events)
✅ Microservices (4 independent processes)
✅ Asynchronous Integration (RabbitMQ)
✅ Idempotency (command + event de-duplication)
✅ Optimistic Concurrency (version-based conflict detection)
✅ Structured Logging (CorrelationId tracing)
✅ Unit Tests (domain + application logic)

All code follows SOLID principles, async/await patterns, and .NET 8 best practices.
