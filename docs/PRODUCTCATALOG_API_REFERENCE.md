# ProductCatalog API Reference

## Command API Reference

### 1. Create Product

**Command**: `CreateProductCommand`

**Request**:
```json
{
  "productId": "prod-12345",
  "sku": "WIDGET-001",
  "name": "Premium Widget",
  "price": 149.99,
  "currency": "USD",
  "description": "High-quality widget with premium features"
}
```

**Response Success**:
```json
{
  "productId": "prod-12345",
  "success": true,
  "error": null
}
```

**Response Error**:
```json
{
  "productId": "prod-12345",
  "success": false,
  "error": "Price cannot be negative"
}
```

**Validation Rules**:
- ProductId: Required, non-empty
- Sku: Required, non-empty, unique
- Name: Required, minimum 2 characters
- Price: Required, >= 0
- Currency: Optional, default "USD"
- Description: Optional

**Emits Event**: `ProductCreatedEvent`

---

### 2. Update Product Details

**Command**: `UpdateProductDetailsCommand`

**Request**:
```json
{
  "productId": "prod-12345",
  "name": "Updated Widget Name",
  "description": "New description with more details"
}
```

**Response Success**:
```json
{
  "success": true,
  "error": null
}
```

**Response Error**:
```json
{
  "success": false,
  "error": "Product not found"
}
```

**Validation Rules**:
- ProductId: Required
- Name: Required, minimum 2 characters
- Description: Optional
- **Constraint**: Product must be active

**Emits Event**: `ProductDetailsUpdatedEvent`

---

### 3. Change Product Price

**Command**: `ChangeProductPriceCommand`

**Request**:
```json
{
  "productId": "prod-12345",
  "newPrice": 129.99,
  "currency": "USD"
}
```

**Response Success**:
```json
{
  "success": true,
  "error": null
}
```

**Response Error**:
```json
{
  "success": false,
  "error": "Product is already inactive"
}
```

**Validation Rules**:
- ProductId: Required
- NewPrice: Required, >= 0
- Currency: Optional, default "USD"
- **Constraint**: Product must be active

**Emits Event**: `ProductPriceChangedEvent` (includes old and new prices)

---

### 4. Activate Product

**Command**: `ActivateProductCommand`

**Request**:
```json
{
  "productId": "prod-12345"
}
```

**Response Success**:
```json
{
  "success": true,
  "error": null
}
```

**Response Error**:
```json
{
  "success": false,
  "error": "Product is already active"
}
```

**Validation Rules**:
- ProductId: Required
- **Constraint**: Product must be inactive to activate

**Emits Event**: `ProductActivatedEvent`

---

### 5. Deactivate Product

**Command**: `DeactivateProductCommand`

**Request**:
```json
{
  "productId": "prod-12345"
}
```

**Response Success**:
```json
{
  "success": true,
  "error": null
}
```

**Response Error**:
```json
{
  "success": false,
  "error": "Product is already inactive"
}
```

**Validation Rules**:
- ProductId: Required
- **Constraint**: Product must be active to deactivate

**Emits Event**: `ProductDeactivatedEvent`

---

## Query API Reference

### 1. Get Product by ID

**Query**: `GetProductByIdQuery`

**Request**:
```json
{
  "productId": "prod-12345"
}
```

**Response Success**:
```json
{
  "productId": "prod-12345",
  "sku": "WIDGET-001",
  "name": "Premium Widget",
  "description": "High-quality widget with premium features",
  "price": 129.99,
  "currency": "USD",
  "isActive": true,
  "createdAt": "2026-01-24T10:30:00Z",
  "lastModifiedAt": "2026-01-24T11:45:00Z"
}
```

**Response Not Found**:
```json
null
```

**Returns**: `ProductDetailView` (complete product details)

---

### 2. List Active Products

**Query**: `ListActiveProductsQuery`

**Request**:
```json
{
  "page": 1,
  "pageSize": 20
}
```

**Response**:
```json
[
  {
    "productId": "prod-12345",
    "name": "Premium Widget",
    "price": 129.99,
    "currency": "USD",
    "isActive": true
  },
  {
    "productId": "prod-12346",
    "name": "Standard Widget",
    "price": 79.99,
    "currency": "USD",
    "isActive": true
  }
]
```

**Parameters**:
- page: Page number (default: 1, min: 1)
- pageSize: Items per page (default: 20, min: 1, max: 100)

**Returns**: `IEnumerable<ProductListView>` (paginated list)

---

### 3. List All Products

**Query**: `ListAllProductsQuery`

**Request**:
```json
{
  "page": 1,
  "pageSize": 20
}
```

**Response**:
```json
[
  {
    "productId": "prod-12345",
    "name": "Premium Widget",
    "price": 129.99,
    "currency": "USD",
    "isActive": true
  },
  {
    "productId": "prod-12346",
    "name": "Discontinued Widget",
    "price": 9.99,
    "currency": "USD",
    "isActive": false
  }
]
```

**Parameters**:
- page: Page number (default: 1, min: 1)
- pageSize: Items per page (default: 20, min: 1, max: 100)

**Returns**: `IEnumerable<ProductListView>` (includes inactive products)

---

### 4. Search Products by Name

**Query**: `SearchProductsByNameQuery`

**Request**:
```json
{
  "searchTerm": "widget",
  "onlyActive": true,
  "page": 1,
  "pageSize": 20
}
```

**Response**:
```json
[
  {
    "productId": "prod-12345",
    "name": "Premium Widget Pro",
    "price": 129.99,
    "currency": "USD",
    "isActive": true
  },
  {
    "productId": "prod-12347",
    "name": "Standard Widget",
    "price": 79.99,
    "currency": "USD",
    "isActive": true
  }
]
```

**Parameters**:
- searchTerm: Text to search for (required)
- onlyActive: Filter to active only (default: true)
- page: Page number (default: 1)
- pageSize: Items per page (default: 20, max: 100)

**Returns**: `IEnumerable<ProductListView>` (search results)

---

## Read Model Schemas

### ProductListView
Used for listing/search views, optimized for common operations.

```csharp
{
  "productId": "string",      // Unique identifier
  "name": "string",           // Product name
  "price": "decimal",         // Current price amount
  "currency": "string",       // Currency code (e.g., "USD")
  "isActive": "boolean"       // Active/inactive status
}
```

### ProductDetailView
Used for detail views, contains complete product information.

```csharp
{
  "productId": "string",          // Unique identifier
  "sku": "string",                // Stock keeping unit
  "name": "string",               // Product name
  "description": "string",        // Product description
  "price": "decimal",             // Current price amount
  "currency": "string",           // Currency code
  "isActive": "boolean",          // Active/inactive status
  "createdAt": "date-time",       // Creation timestamp
  "lastModifiedAt": "date-time"   // Last update timestamp
}
```

---

## Domain Events

### ProductCreatedEvent
Emitted when a new product is created.

**Payload**:
```json
{
  "eventId": "uuid",
  "eventType": "ECommerceMvp.ProductCatalog.Domain.ProductCreatedEvent",
  "eventVersion": 1,
  "occurredAt": "2026-01-24T10:30:00Z",
  "productId": "prod-12345",
  "sku": "WIDGET-001",
  "name": "Premium Widget",
  "price": 149.99,
  "currency": "USD",
  "description": "High-quality widget with premium features"
}
```

---

### ProductDetailsUpdatedEvent
Emitted when product details are updated.

**Payload**:
```json
{
  "eventId": "uuid",
  "eventType": "ECommerceMvp.ProductCatalog.Domain.ProductDetailsUpdatedEvent",
  "eventVersion": 1,
  "occurredAt": "2026-01-24T11:00:00Z",
  "productId": "prod-12345",
  "name": "Updated Widget Name",
  "description": "Updated description"
}
```

---

### ProductPriceChangedEvent
Emitted when product price is changed.

**Payload**:
```json
{
  "eventId": "uuid",
  "eventType": "ECommerceMvp.ProductCatalog.Domain.ProductPriceChangedEvent",
  "eventVersion": 1,
  "occurredAt": "2026-01-24T11:15:00Z",
  "productId": "prod-12345",
  "oldPrice": 149.99,
  "newPrice": 129.99,
  "oldCurrency": "USD",
  "newCurrency": "USD"
}
```

---

### ProductActivatedEvent
Emitted when a product is activated.

**Payload**:
```json
{
  "eventId": "uuid",
  "eventType": "ECommerceMvp.ProductCatalog.Domain.ProductActivatedEvent",
  "eventVersion": 1,
  "occurredAt": "2026-01-24T11:30:00Z",
  "productId": "prod-12345"
}
```

---

### ProductDeactivatedEvent
Emitted when a product is deactivated.

**Payload**:
```json
{
  "eventId": "uuid",
  "eventType": "ECommerceMvp.ProductCatalog.Domain.ProductDeactivatedEvent",
  "eventVersion": 1,
  "occurredAt": "2026-01-24T11:45:00Z",
  "productId": "prod-12345"
}
```

---

## Error Codes & Messages

| Error | Meaning | HTTP Status |
|-------|---------|-------------|
| ProductId is required | Missing product identifier | 400 |
| SKU is required | Missing SKU | 400 |
| Name is required | Missing product name | 400 |
| Price cannot be negative | Invalid price value | 400 |
| Product not found | Product doesn't exist | 404 |
| Cannot update an inactive product | Only active products can be updated | 422 |
| Cannot change price of an inactive product | Only active products price can be changed | 422 |
| Product is already active | Cannot activate already active product | 422 |
| Product is already inactive | Cannot deactivate already inactive product | 422 |

---

## Pagination Guidelines

For all paginated queries:
- **Default page size**: 20
- **Maximum page size**: 100
- **Minimum page number**: 1
- Invalid values are automatically corrected to defaults

**Example**:
```
GET /api/products?page=2&pageSize=50
```

Returns items 51-100 (0-based offset calculation).

---

## Usage Examples

### Example 1: Create and Activate a Product

```bash
# 1. Create product
POST /api/commands/create-product
{
  "productId": "prod-001",
  "sku": "SKU-001",
  "name": "My Product",
  "price": 99.99,
  "description": "Product description"
}
→ Response: { "success": true, "productId": "prod-001" }

# 2. Activate product
POST /api/commands/activate-product
{
  "productId": "prod-001"
}
→ Response: { "success": true }

# 3. Query active products
GET /api/queries/list-active-products?page=1&pageSize=20
→ Response: [{ "productId": "prod-001", "name": "My Product", ... }]
```

### Example 2: Update Product Price

```bash
# 1. Change price
POST /api/commands/change-price
{
  "productId": "prod-001",
  "newPrice": 79.99
}
→ Response: { "success": true }

# 2. Verify in read model
GET /api/queries/get-product-by-id?productId=prod-001
→ Response: { "productId": "prod-001", "price": 79.99, ... }
```

### Example 3: Search Products

```bash
# Search for products matching "widget"
GET /api/queries/search-products?searchTerm=widget&onlyActive=true&page=1

→ Response: 
[
  {
    "productId": "prod-123",
    "name": "Premium Widget Pro",
    "price": 129.99,
    "isActive": true
  },
  ...
]
```
