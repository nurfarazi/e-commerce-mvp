# Order API Reference

## Command API (Port 5006)

### Base URL
```
http://localhost:5006/api
```

### Endpoints

#### Place Order
Places a new order from a shopping cart with idempotency support.

```
POST /Orders/place-order
```

**Request Headers:**
```
Content-Type: application/json
```

**Request Body:**
```json
{
  "guestToken": "string (required, non-empty)",
  "cartId": "string (required, non-empty)",
  "idempotencyKey": "string (required, non-empty, unique per order)",
  "customerInfo": {
    "name": "string (required, min 2 chars)",
    "phone": "string (required, non-empty)",
    "email": "string (optional, valid email if provided)"
  },
  "shippingAddress": {
    "line1": "string (required, non-empty)",
    "line2": "string (optional)",
    "city": "string (required, non-empty)",
    "postalCode": "string (optional)",
    "country": "string (optional, defaults to 'US')"
  },
  "cartItems": [
    {
      "productId": "string (required)",
      "quantity": "integer (required, > 0)"
    }
  ],
  "productSnapshots": [
    {
      "productId": "string (required)",
      "sku": "string (required)",
      "name": "string (required)",
      "price": "decimal (required, >= 0)",
      "currency": "string (required, defaults to 'USD')",
      "isActive": "boolean (required, must be true)"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "orderId": "string (uuid)",
  "orderNumber": "string (ORD-YYYYMMDD-XXXXX)",
  "success": "boolean",
  "error": null
}
```

**Response (200 OK - Validation Error):**
```json
{
  "orderId": null,
  "orderNumber": null,
  "success": false,
  "error": "string (error message)"
}
```

**Status Codes:**
- `200 OK`: Request processed (check success flag)
- `400 Bad Request`: Invalid request format
- `500 Internal Server Error`: Unexpected error

**Validation Errors:**
- "OrderId is required"
- "GuestToken is required"
- "CartId is required"
- "IdempotencyKey is required"
- "CustomerInfo is required"
- "Customer name is required"
- "Customer name must be at least 2 characters"
- "Customer phone is required"
- "Invalid email format"
- "ShippingAddress is required"
- "Address line 1 is required"
- "City is required"
- "CartItems is empty"
- "ProductSnapshots is required"
- "Product {productId} not found in snapshots"
- "Product {productId} ({sku}) is not active"
- "IDEMPOTENCY_CONFLICT: Order already placed with this key"

**Example Request:**
```bash
curl -X POST http://localhost:5006/api/Orders/place-order \
  -H "Content-Type: application/json" \
  -d '{
    "guestToken": "guest-abc-123",
    "cartId": "cart-def-456",
    "idempotencyKey": "order-ghi-789",
    "customerInfo": {
      "name": "John Smith",
      "phone": "+1-800-123-4567",
      "email": "john@example.com"
    },
    "shippingAddress": {
      "line1": "123 Main St",
      "city": "Springfield",
      "postalCode": "12345"
    },
    "cartItems": [
      {"productId": "prod-001", "quantity": 2}
    ],
    "productSnapshots": [
      {
        "productId": "prod-001",
        "sku": "WIDGET-001",
        "name": "Widget",
        "price": 19.99,
        "currency": "USD",
        "isActive": true
      }
    ]
  }'
```

---

## Query API (Port 5007)

### Base URL
```
http://localhost:5007/api
```

### Endpoints

#### Get Order Detail by ID
Retrieves complete order details by order ID.

```
GET /OrderQueries/{orderId}
```

**Path Parameters:**
- `orderId`: string (uuid, required)

**Response (200 OK):**
```json
{
  "orderId": "string",
  "orderNumber": "string",
  "guestToken": "string",
  "customerInfo": {
    "name": "string",
    "phone": "string",
    "email": "string or null"
  },
  "shippingAddress": {
    "line1": "string",
    "line2": "string or null",
    "city": "string",
    "postalCode": "string or null",
    "country": "string"
  },
  "lineItems": [
    {
      "lineItemId": "string",
      "productId": "string",
      "skuSnapshot": "string",
      "nameSnapshot": "string",
      "unitPriceSnapshot": "decimal",
      "quantity": "integer",
      "lineTotal": "decimal"
    }
  ],
  "totals": {
    "subtotal": "decimal",
    "shippingFee": "decimal (always 0)",
    "total": "decimal",
    "currency": "string"
  },
  "paymentMethod": "string (always 'COD')",
  "paymentStatus": "string (always 'Pending')",
  "status": "string ('Created'|'Validated'|'Priced'|'StockCommitRequested'|'StockCommitted'|'Finalized')",
  "stockCommitted": "boolean",
  "createdAt": "datetime (ISO 8601)"
}
```

**Response (404 Not Found):**
```
Empty response body
```

**Status Codes:**
- `200 OK`: Order found
- `404 Not Found`: Order not found

**Example Request:**
```bash
curl http://localhost:5007/api/OrderQueries/123e4567-e89b-12d3-a456-426614174000
```

---

#### Get Order Detail by Order Number
Retrieves complete order details by human-readable order number.

```
GET /OrderQueries/by-number/{orderNumber}
```

**Path Parameters:**
- `orderNumber`: string (format: ORD-YYYYMMDD-XXXXX, required)

**Response:** Same as Get Order Detail by ID

**Status Codes:**
- `200 OK`: Order found
- `404 Not Found`: Order not found

**Example Request:**
```bash
curl http://localhost:5007/api/OrderQueries/by-number/ORD-20250124-54321
```

---

#### Get All Orders (Admin)
Retrieves summary list of all orders for admin dashboard.

```
GET /OrderQueries/admin/orders
```

**Response (200 OK):**
```json
[
  {
    "orderId": "string",
    "orderNumber": "string",
    "customerName": "string",
    "customerPhone": "string",
    "total": "decimal",
    "currency": "string",
    "createdAt": "datetime (ISO 8601)",
    "status": "string"
  }
]
```

**Response (200 OK - Empty):**
```json
[]
```

**Status Codes:**
- `200 OK`: Request successful (may return empty list)

**Example Request:**
```bash
curl http://localhost:5007/api/OrderQueries/admin/orders
```

---

## Data Types

### CustomerInfo
```json
{
  "name": "string (2-255 characters)",
  "phone": "string (non-empty)",
  "email": "string (optional, valid email format)"
}
```

### ShippingAddress
```json
{
  "line1": "string (required)",
  "line2": "string (optional)",
  "city": "string (required)",
  "postalCode": "string (optional)",
  "country": "string (defaults to 'US')"
}
```

### CartItem
```json
{
  "productId": "string",
  "quantity": "integer (> 0)"
}
```

### ProductSnapshot
```json
{
  "productId": "string",
  "sku": "string",
  "name": "string",
  "price": "decimal (>= 0)",
  "currency": "string (defaults to 'USD')",
  "isActive": "boolean"
}
```

### OrderLineItem
```json
{
  "lineItemId": "string",
  "productId": "string",
  "skuSnapshot": "string",
  "nameSnapshot": "string",
  "unitPriceSnapshot": "decimal",
  "quantity": "integer",
  "lineTotal": "decimal"
}
```

### OrderTotals
```json
{
  "subtotal": "decimal",
  "shippingFee": "decimal (always 0 in MVP)",
  "total": "decimal",
  "currency": "string"
}
```

### Order Status
- `Created`: Order just created, awaiting validation
- `Validated`: All invariants verified
- `Priced`: Line items and totals calculated
- `StockCommitRequested`: Awaiting inventory confirmation
- `StockCommitted`: Stock reserved/deducted
- `Finalized`: Order ready for fulfillment

---

## Order Number Format
```
ORD-YYYYMMDD-XXXXX
```

Where:
- `ORD` = Fixed prefix
- `YYYYMMDD` = Order date (e.g., 20250124)
- `XXXXX` = Random 5-digit number (10000-99999)

Example: `ORD-20250124-54321`

---

## Idempotency

The Place Order endpoint supports idempotency to prevent duplicate orders.

**Rules:**
- Each order request must include a unique `idempotencyKey`
- Same key value cannot create multiple orders
- If same key is used again, returns `IDEMPOTENCY_CONFLICT` error
- Idempotency key should be UUID or unique identifier

**Example:**
```bash
# First call - returns orderId
POST /api/Orders/place-order
{"idempotencyKey": "unique-key-1", ...}

# Second call with same key - returns error
POST /api/Orders/place-order
{"idempotencyKey": "unique-key-1", ...}
→ Error: "IDEMPOTENCY_CONFLICT: Order already placed with this key"
```

---

## HTTP Methods & Status Codes

| Method | Endpoint | Status | Meaning |
|--------|----------|--------|---------|
| POST | /Orders/place-order | 200 | Order processed (check success flag) |
| POST | /Orders/place-order | 400 | Invalid request |
| POST | /Orders/place-order | 500 | Server error |
| GET | /OrderQueries/{orderId} | 200 | Order found |
| GET | /OrderQueries/{orderId} | 404 | Order not found |
| GET | /OrderQueries/by-number/{orderNumber} | 200 | Order found |
| GET | /OrderQueries/by-number/{orderNumber} | 404 | Order not found |
| GET | /OrderQueries/admin/orders | 200 | Success (may be empty) |

---

## Content Types
- **Request**: `application/json`
- **Response**: `application/json`

---

## Error Response Format

All errors return JSON response with error message:

```json
{
  "orderId": null,
  "orderNumber": null,
  "success": false,
  "error": "string description of error"
}
```

---

## Examples

### Complete Order Placement Workflow

1. **Place Order**
```bash
curl -X POST http://localhost:5006/api/Orders/place-order \
  -H "Content-Type: application/json" \
  -d '{
    "guestToken": "guest-session-123",
    "cartId": "cart-abc-def",
    "idempotencyKey": "order-123-456-789",
    "customerInfo": {
      "name": "Alice Johnson",
      "phone": "+1-555-0100",
      "email": "alice@example.com"
    },
    "shippingAddress": {
      "line1": "456 Oak Lane",
      "line2": "Suite 100",
      "city": "Boston",
      "postalCode": "02101",
      "country": "US"
    },
    "cartItems": [
      {"productId": "prod-abc", "quantity": 1},
      {"productId": "prod-def", "quantity": 3}
    ],
    "productSnapshots": [
      {
        "productId": "prod-abc",
        "sku": "SKU-001",
        "name": "Premium Widget",
        "price": 99.99,
        "currency": "USD",
        "isActive": true
      },
      {
        "productId": "prod-def",
        "sku": "SKU-002",
        "name": "Standard Gadget",
        "price": 29.99,
        "currency": "USD",
        "isActive": true
      }
    ]
  }'
```

Response:
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "orderNumber": "ORD-20250124-87654",
  "success": true,
  "error": null
}
```

2. **Get Order Details**
```bash
curl http://localhost:5007/api/OrderQueries/550e8400-e29b-41d4-a716-446655440000
```

Response:
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "orderNumber": "ORD-20250124-87654",
  "guestToken": "guest-session-123",
  "customerInfo": {
    "name": "Alice Johnson",
    "phone": "+1-555-0100",
    "email": "alice@example.com"
  },
  "shippingAddress": {
    "line1": "456 Oak Lane",
    "line2": "Suite 100",
    "city": "Boston",
    "postalCode": "02101",
    "country": "US"
  },
  "lineItems": [
    {
      "lineItemId": "item-1",
      "productId": "prod-abc",
      "skuSnapshot": "SKU-001",
      "nameSnapshot": "Premium Widget",
      "unitPriceSnapshot": 99.99,
      "quantity": 1,
      "lineTotal": 99.99
    },
    {
      "lineItemId": "item-2",
      "productId": "prod-def",
      "skuSnapshot": "SKU-002",
      "nameSnapshot": "Standard Gadget",
      "unitPriceSnapshot": 29.99,
      "quantity": 3,
      "lineTotal": 89.97
    }
  ],
  "totals": {
    "subtotal": 189.96,
    "shippingFee": 0.00,
    "total": 189.96,
    "currency": "USD"
  },
  "paymentMethod": "COD",
  "paymentStatus": "Pending",
  "status": "Created",
  "stockCommitted": false,
  "createdAt": "2025-01-24T14:30:00.000Z"
}
```

3. **Get by Order Number**
```bash
curl http://localhost:5007/api/OrderQueries/by-number/ORD-20250124-87654
```

Response: Same as Get Order Details

4. **List All Orders (Admin)**
```bash
curl http://localhost:5007/api/OrderQueries/admin/orders
```

Response:
```json
[
  {
    "orderId": "550e8400-e29b-41d4-a716-446655440000",
    "orderNumber": "ORD-20250124-87654",
    "customerName": "Alice Johnson",
    "customerPhone": "+1-555-0100",
    "total": 189.96,
    "currency": "USD",
    "createdAt": "2025-01-24T14:30:00.000Z",
    "status": "Created"
  }
]
```

---

## Rate Limiting
Not implemented in MVP. Add in production.

## Authentication
Not implemented in MVP. Add in production (OAuth/JWT).

## Versioning
Current API version: v1 (implicit in endpoints)

## Support & Troubleshooting

**Common Issues:**

1. **"Request body is required"**
   - Ensure POST request includes valid JSON body

2. **"IDEMPOTENCY_CONFLICT"**
   - Use a new `idempotencyKey` for each order
   - Or wait for backend to forget the key (configurable TTL)

3. **"Product {id} not found in snapshots"**
   - Include all cart items in productSnapshots array
   - Ensure productId matches exactly

4. **"Product is not active"**
   - Only active products can be ordered
   - Check `isActive: true` in productSnapshots

5. **404 Order Not Found**
   - Verify orderId or orderNumber is correct
   - May take time for read model to update
