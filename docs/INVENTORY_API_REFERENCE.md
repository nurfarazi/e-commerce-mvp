# Inventory API Reference

## Base URLs
- **CommandApi**: http://localhost:5002/api/inventory
- **QueryApi**: http://localhost:5003/api/inventory

## CommandApi Endpoints

### 1. Set Stock (Admin Operation)
```
POST /api/inventory/{productId}/set-stock
```
**Description**: Set stock quantity for a product (creates if doesn't exist).

**Parameters**:
- `productId` (path): Product identifier

**Request Body**:
```json
{
  "newQuantity": 100,
  "reason": "Initial stock setup",
  "changedBy": "admin@example.com"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "error": null
}
```

**Error Responses**:
- `400 Bad Request`: Invalid input (negative quantity, missing productId, etc.)
- `500 Internal Server Error`: Server error

**Example**:
```bash
curl -X POST http://localhost:5002/api/inventory/PROD-001/set-stock \
  -H "Content-Type: application/json" \
  -d '{
    "newQuantity": 100,
    "reason": "Initial stock",
    "changedBy": "admin"
  }'
```

---

### 2. Validate Stock Availability
```
POST /api/inventory/validate-stock
```
**Description**: Check if requested quantities are available for multiple products (non-mutating).

**Request Body**:
```json
{
  "items": [
    {
      "productId": "PROD-001",
      "requestedQuantity": 5
    },
    {
      "productId": "PROD-002",
      "requestedQuantity": 3
    }
  ]
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "results": [
    {
      "productId": "PROD-001",
      "requestedQuantity": 5,
      "availableQuantity": 50,
      "isAvailable": true
    },
    {
      "productId": "PROD-002",
      "requestedQuantity": 3,
      "availableQuantity": 2,
      "isAvailable": false
    }
  ],
  "error": null
}
```

**Example**:
```bash
curl -X POST http://localhost:5002/api/inventory/validate-stock \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      { "productId": "PROD-001", "requestedQuantity": 5 },
      { "productId": "PROD-002", "requestedQuantity": 3 }
    ]
  }'
```

---

### 3. Deduct Stock for Order
```
POST /api/inventory/deduct-for-order
```
**Description**: Atomically deduct stock for an order. Idempotent per order - same orderId/productId won't double-deduct.

**Request Body**:
```json
{
  "orderId": "ORD-12345",
  "items": [
    {
      "productId": "PROD-001",
      "quantity": 2
    },
    {
      "productId": "PROD-002",
      "quantity": 1
    }
  ]
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "results": [
    {
      "productId": "PROD-001",
      "quantityDeducted": 2,
      "remainingQuantity": 48,
      "success": true,
      "error": null
    },
    {
      "productId": "PROD-002",
      "quantityDeducted": 0,
      "remainingQuantity": 2,
      "success": false,
      "error": "Insufficient inventory for product PROD-002: requested 1, available 2"
    }
  ],
  "error": null
}
```

**Error Responses**:
- `400 Bad Request`: Invalid input (missing orderId, empty items, etc.)
- `500 Internal Server Error`: Server error

**Example**:
```bash
curl -X POST http://localhost:5002/api/inventory/deduct-for-order \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-12345",
    "items": [
      { "productId": "PROD-001", "quantity": 2 },
      { "productId": "PROD-002", "quantity": 1 }
    ]
  }'
```

---

## QueryApi Endpoints

### 1. Get Stock Availability for Single Product
```
GET /api/inventory/{productId}/availability
```
**Description**: Get current stock availability for a specific product.

**Parameters**:
- `productId` (path): Product identifier

**Response** (200 OK):
```json
{
  "productId": "PROD-001",
  "availableQuantity": 50,
  "inStockFlag": true,
  "lastUpdatedAt": "2024-01-24T10:30:45.123Z"
}
```

**Error Responses**:
- `404 Not Found`: Product not found in inventory
- `500 Internal Server Error`: Server error

**Example**:
```bash
curl http://localhost:5003/api/inventory/PROD-001/availability
```

---

### 2. Get Stock Availability for Multiple Products
```
POST /api/inventory/availability/batch
```
**Description**: Get stock availability for multiple products in a single request.

**Request Body**:
```json
{
  "productIds": ["PROD-001", "PROD-002", "PROD-003"]
}
```

**Response** (200 OK):
```json
[
  {
    "productId": "PROD-001",
    "availableQuantity": 50,
    "inStockFlag": true,
    "lastUpdatedAt": "2024-01-24T10:30:45.123Z"
  },
  {
    "productId": "PROD-002",
    "availableQuantity": 2,
    "inStockFlag": true,
    "lastUpdatedAt": "2024-01-24T10:25:30.456Z"
  }
]
```

**Example**:
```bash
curl -X POST http://localhost:5003/api/inventory/availability/batch \
  -H "Content-Type: application/json" \
  -d '{ "productIds": ["PROD-001", "PROD-002"] }'
```

---

### 3. Check if Product is In Stock
```
GET /api/inventory/{productId}/in-stock
```
**Description**: Quick check if a product is in stock.

**Parameters**:
- `productId` (path): Product identifier

**Response** (200 OK):
```json
{
  "productId": "PROD-001",
  "inStock": true,
  "availableQuantity": 50
}
```

**Error Responses**:
- `404 Not Found`: Product not found
- `500 Internal Server Error`: Server error

**Example**:
```bash
curl http://localhost:5003/api/inventory/PROD-001/in-stock
```

---

### 4. Get All Low Stock Products
```
GET /api/inventory/low-stock
```
**Description**: Retrieve all products with quantity below low stock threshold (10 units).

**Response** (200 OK):
```json
[
  {
    "productId": "PROD-002",
    "availableQuantity": 5,
    "lowStockThreshold": 10,
    "isLow": true,
    "alertedAt": "2024-01-24T09:15:20.789Z"
  },
  {
    "productId": "PROD-005",
    "availableQuantity": 8,
    "lowStockThreshold": 10,
    "isLow": true,
    "alertedAt": "2024-01-24T09:10:15.456Z"
  }
]
```

**Error Responses**:
- `500 Internal Server Error`: Server error

**Example**:
```bash
curl http://localhost:5003/api/inventory/low-stock
```

---

## Status Codes

| Code | Meaning |
|------|---------|
| 200 | Successful request |
| 400 | Bad request (invalid input) |
| 404 | Resource not found |
| 500 | Internal server error |

---

## Request/Response Models

### SetStockRequest
```csharp
public class SetStockRequest
{
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}
```

### SetStockResponse
```csharp
public class SetStockResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

### ValidateStockRequest
```csharp
public class ValidateStockRequest
{
    public List<ValidateStockItem> Items { get; set; }
}

public class ValidateStockItem
{
    public string ProductId { get; set; }
    public int RequestedQuantity { get; set; }
}
```

### ValidateStockResponse
```csharp
public class ValidateStockResponse
{
    public bool Success { get; set; }
    public List<StockValidationResult> Results { get; set; }
    public string? Error { get; set; }
}

public class StockValidationResult
{
    public string ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}
```

### DeductStockForOrderRequest
```csharp
public class DeductStockForOrderRequest
{
    public string OrderId { get; set; }
    public List<DeductStockItem> Items { get; set; }
}

public class DeductStockItem
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
}
```

### DeductStockForOrderResponse
```csharp
public class DeductStockForOrderResponse
{
    public bool Success { get; set; }
    public List<DeductionResult> Results { get; set; }
    public string? Error { get; set; }
}

public class DeductionResult
{
    public string ProductId { get; set; }
    public int QuantityDeducted { get; set; }
    public int RemainingQuantity { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

### StockAvailabilityView
```csharp
public class StockAvailabilityView
{
    public string ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public bool InStockFlag { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### LowStockView
```csharp
public class LowStockView
{
    public string ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsLow { get; set; }
    public DateTime AlertedAt { get; set; }
}
```

---

## Testing Flow

1. **Set Initial Stock**
   ```bash
   POST /api/inventory/PROD-001/set-stock
   { "newQuantity": 100 }
   ```

2. **Validate Availability**
   ```bash
   POST /api/inventory/validate-stock
   { "items": [{ "productId": "PROD-001", "requestedQuantity": 50 }] }
   ```

3. **Query Availability**
   ```bash
   GET /api/inventory/PROD-001/availability
   ```

4. **Deduct for Order**
   ```bash
   POST /api/inventory/deduct-for-order
   { "orderId": "ORD-001", "items": [{ "productId": "PROD-001", "quantity": 30 }] }
   ```

5. **Verify Updated Stock**
   ```bash
   GET /api/inventory/PROD-001/availability
   ```
