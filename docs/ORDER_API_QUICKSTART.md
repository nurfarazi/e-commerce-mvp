# Order API Quickstart Guide

## Overview
This guide shows how to use the OrderManagement API to place orders and retrieve order information.

## Starting the Services

### Using Docker Compose
```bash
docker-compose up -d
```

This will start:
- `order-commandapi` on http://localhost:5006
- `order-queryapi` on http://localhost:5007

### Swagger Documentation
- Command API: http://localhost:5006/swagger
- Query API: http://localhost:5007/swagger

## API Endpoints

### Place Order (Command API)
```http
POST http://localhost:5006/api/Orders/place-order
Content-Type: application/json

{
  "guestToken": "guest-123-abc",
  "cartId": "cart-456-def",
  "idempotencyKey": "idempotency-789-ghi",
  "customerInfo": {
    "name": "John Doe",
    "phone": "+1-555-0123",
    "email": "john.doe@example.com"
  },
  "shippingAddress": {
    "line1": "123 Main Street",
    "line2": "Apt 4B",
    "city": "New York",
    "postalCode": "10001",
    "country": "US"
  },
  "cartItems": [
    {
      "productId": "product-001",
      "quantity": 2
    },
    {
      "productId": "product-002",
      "quantity": 1
    }
  ],
  "productSnapshots": [
    {
      "productId": "product-001",
      "sku": "SKU-001",
      "name": "Widget A",
      "price": 29.99,
      "currency": "USD",
      "isActive": true
    },
    {
      "productId": "product-002",
      "sku": "SKU-002",
      "name": "Gadget B",
      "price": 49.99,
      "currency": "USD",
      "isActive": true
    }
  ]
}
```

#### Response (Success)
```json
{
  "orderId": "order-uuid-123",
  "orderNumber": "ORD-20250124-54321",
  "success": true,
  "error": null
}
```

#### Response (Error)
```json
{
  "orderId": null,
  "orderNumber": null,
  "success": false,
  "error": "Customer name is required"
}
```

### Get Order Details (Query API)
```http
GET http://localhost:5007/api/OrderQueries/{orderId}
```

Example:
```bash
curl http://localhost:5007/api/OrderQueries/order-uuid-123
```

Response:
```json
{
  "orderId": "order-uuid-123",
  "orderNumber": "ORD-20250124-54321",
  "guestToken": "guest-123-abc",
  "customerInfo": {
    "name": "John Doe",
    "phone": "+1-555-0123",
    "email": "john.doe@example.com"
  },
  "shippingAddress": {
    "line1": "123 Main Street",
    "line2": "Apt 4B",
    "city": "New York",
    "postalCode": "10001",
    "country": "US"
  },
  "lineItems": [
    {
      "lineItemId": "line-item-uuid-1",
      "productId": "product-001",
      "skuSnapshot": "SKU-001",
      "nameSnapshot": "Widget A",
      "unitPriceSnapshot": 29.99,
      "quantity": 2,
      "lineTotal": 59.98
    },
    {
      "lineItemId": "line-item-uuid-2",
      "productId": "product-002",
      "skuSnapshot": "SKU-002",
      "nameSnapshot": "Gadget B",
      "unitPriceSnapshot": 49.99,
      "quantity": 1,
      "lineTotal": 49.99
    }
  ],
  "totals": {
    "subtotal": 109.97,
    "shippingFee": 0.00,
    "total": 109.97,
    "currency": "USD"
  },
  "paymentMethod": "COD",
  "paymentStatus": "Pending",
  "status": "Created",
  "stockCommitted": false,
  "createdAt": "2025-01-24T10:30:45.123Z"
}
```

### Get Order by Order Number (Query API)
```http
GET http://localhost:5007/api/OrderQueries/by-number/{orderNumber}
```

Example:
```bash
curl http://localhost:5007/api/OrderQueries/by-number/ORD-20250124-54321
```

### Get All Orders (Admin) (Query API)
```http
GET http://localhost:5007/api/OrderQueries/admin/orders
```

Response:
```json
[
  {
    "orderId": "order-uuid-123",
    "orderNumber": "ORD-20250124-54321",
    "customerName": "John Doe",
    "customerPhone": "+1-555-0123",
    "total": 109.97,
    "currency": "USD",
    "createdAt": "2025-01-24T10:30:45.123Z",
    "status": "Created"
  },
  {
    "orderId": "order-uuid-456",
    "orderNumber": "ORD-20250124-12345",
    "customerName": "Jane Smith",
    "customerPhone": "+1-555-0456",
    "total": 249.97,
    "currency": "USD",
    "createdAt": "2025-01-24T11:15:22.456Z",
    "status": "StockCommitted"
  }
]
```

## Idempotency

The Order API supports idempotency to prevent duplicate order creation. Use the same `idempotencyKey` for the same order request:

```bash
# First request
POST http://localhost:5006/api/Orders/place-order
{
  "idempotencyKey": "unique-key-001",
  ...
}
# Returns: orderId, orderNumber

# Retry with same key (even days later)
POST http://localhost:5006/api/Orders/place-order
{
  "idempotencyKey": "unique-key-001",
  ...
}
# Returns: IDEMPOTENCY_CONFLICT error (already processed)
```

## Validation Rules

### Customer Info
- `name`: Required, minimum 2 characters, max 255 characters
- `phone`: Required, non-empty string
- `email`: Optional, must be valid email format if provided

### Shipping Address
- `line1`: Required, non-empty street address
- `line2`: Optional, additional address info
- `city`: Required, non-empty city name
- `postalCode`: Optional, zip/postal code
- `country`: Defaults to "US" if not provided

### Cart & Products
- Must have at least 1 item in cart
- All products must be active (`isActive: true`)
- All products must have valid prices (>= 0)
- Product snapshots required for pricing

### Order Constraints
- Shipping fee always 0 (MVP)
- Payment method fixed to COD (Cash On Delivery)
- Payment status fixed to Pending (waiting for payment)
- Same idempotency key cannot create multiple orders

## Error Handling

### Common Errors

| Status | Error | Description |
|--------|-------|-------------|
| 400 | "Request body is required" | No JSON body sent |
| 400 | "OrderId is required" | Missing orderId field |
| 400 | "GuestToken is required" | Missing guestToken field |
| 400 | "Customer name is required" | Missing/empty name |
| 400 | "Customer phone is required" | Missing/empty phone |
| 400 | "Address line 1 is required" | Missing address line1 |
| 400 | "City is required" | Missing city |
| 400 | "Cart is empty" | No items in cart |
| 400 | "Product {id} not found in snapshots" | Product snapshot missing |
| 400 | "Product {id} is not active" | Product not available for sale |
| 400 | "IDEMPOTENCY_CONFLICT: Order already placed with this key" | Duplicate order attempt |
| 404 | Not Found | Order not found in query API |

## Example cURL Commands

### Place Order
```bash
curl -X POST http://localhost:5006/api/Orders/place-order \
  -H "Content-Type: application/json" \
  -d '{
    "guestToken": "guest-123",
    "cartId": "cart-456",
    "idempotencyKey": "order-789",
    "customerInfo": {
      "name": "Alice Johnson",
      "phone": "+1-555-9999",
      "email": "alice@example.com"
    },
    "shippingAddress": {
      "line1": "456 Oak Avenue",
      "city": "Los Angeles",
      "country": "US"
    },
    "cartItems": [
      {"productId": "prod-1", "quantity": 1}
    ],
    "productSnapshots": [
      {
        "productId": "prod-1",
        "sku": "SKU-100",
        "name": "Product One",
        "price": 99.99,
        "currency": "USD",
        "isActive": true
      }
    ]
  }'
```

### Get Order by ID
```bash
curl http://localhost:5007/api/OrderQueries/order-uuid-123
```

### Get Order by Number
```bash
curl http://localhost:5007/api/OrderQueries/by-number/ORD-20250124-54321
```

### Get All Orders
```bash
curl http://localhost:5007/api/OrderQueries/admin/orders
```

## Testing with Postman

1. Import the API collection from Swagger: http://localhost:5006/swagger
2. Create variables:
   - `orderId`: auto-populate with UUID
   - `guestToken`: use any string (e.g., "guest-123")
   - `cartId`: use any string (e.g., "cart-456")
   - `idempotencyKey`: use unique string per test

3. Test flow:
   - Place Order → Copy orderId from response
   - Get Order Details (by ID) → Verify order
   - Get Order (by number) → Verify order
   - Get All Orders → See in list

## Event Flow

When an order is placed:

1. **PlaceOrderCommand** received
2. **OrderPlacementRequested** event emitted (audit trail)
3. **OrderValidated** event emitted (all invariants checked)
4. **OrderPriced** event emitted (items and totals calculated)
5. **OrderCreated** event emitted (full order snapshot)
6. **OrderStockCommitRequested** event emitted (inventory trigger)
7. **OrderSubmitted** integration event published (Inventory, Cart listeners)
8. Order saved to repository
9. Events published to message broker
10. **OrderDetailView** read model updated
11. **AdminOrderListView** read model updated

## Architecture

```
Client
  ↓
Order Command API (port 5006)
  ↓
PlaceOrderCommand Handler
  ↓
Order Domain (validation, event emission)
  ↓
Order Repository (save)
  ↓
Event Publisher (broadcast)
  ↓
Event Handlers
  ├→ OrderDetailView updater
  ├→ AdminOrderListView updater
  ├→ Inventory notification
  └→ Cart notification
  ↓
Order Query API (port 5007)
  ↓
Query Handlers
  ├→ OrderDetailView reader
  ├→ AdminOrderListView reader
  └→ Order lookup by number
  ↓
Client
```

## Production Considerations

- Add authentication (OAuth, JWT)
- Add request validation middleware
- Add error tracking (Sentry, AppInsights)
- Add API rate limiting
- Add distributed tracing (Jaeger, OpenTelemetry)
- Replace in-memory stores with databases
- Implement event persistence
- Add order status notifications
- Implement payment processing
- Add order fulfillment workflows
