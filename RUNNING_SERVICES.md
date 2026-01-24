# Running All Services

This document explains how to run all microservices and infrastructure components for the E-Commerce MVP application.

## Options

### 1. Run Everything with Docker Compose (Recommended)

All services, including infrastructure (MongoDB, RabbitMQ), will run in Docker containers.

```bash
docker-compose up -d
```

This will start:
- **MongoDB**: Port 27017
- **RabbitMQ**: Port 5672 (AMQP), 15672 (Management UI)
- **ProductCatalog CommandApi**: Port 5000
- **ProductCatalog QueryApi**: Port 5001
- **ProductCatalog CommandHandler**: Background service
- **ProductCatalog EventHandler**: Background service
- **Inventory CommandApi**: Port 5002
- **Inventory QueryApi**: Port 5003
- **Inventory CommandHandler**: Background service
- **Inventory EventHandler**: Background service

#### Stop all services:
```bash
docker-compose down
```

#### View logs:
```bash
docker-compose logs -f [service_name]
# Example: docker-compose logs -f productcatalog-commandapi
```

---

### 2. Run with the Shell Script (Mixed Setup)

Infrastructure runs in Docker, .NET services run locally (useful for development).

#### Prerequisites:
- Docker and Docker Compose installed
- .NET 8.0 and .NET 10.0 SDKs installed
- macOS/Linux with bash

#### Run everything:
```bash
./run-all.sh
```

#### Build only (no services):
```bash
./run-all.sh --build-only
```

#### Docker infrastructure only:
```bash
./run-all.sh --docker-only
```

#### Clean build (remove all bin/obj):
```bash
./run-all.sh --clean
```

#### Combine options:
```bash
./run-all.sh --clean --docker-only
```

---

## Service Endpoints

### ProductCatalog Services
- **Command API**: http://localhost:5000
- **Query API**: http://localhost:5001

### Inventory Services
- **Command API**: http://localhost:5002
- **Query API**: http://localhost:5003

### Infrastructure
- **MongoDB**: mongodb://admin:admin@localhost:27017
- **RabbitMQ AMQP**: amqp://guest:guest@localhost:5672
- **RabbitMQ Management**: http://localhost:15672 (guest/guest)

---

## Example API Calls

### Create a Product (ProductCatalog)
```bash
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Laptop",
    "description": "High-performance laptop",
    "price": 1299.99
  }'
```

### Get Products (ProductCatalog)
```bash
curl http://localhost:5001/api/products
```

### Set Stock (Inventory)
```bash
curl -X POST http://localhost:5002/api/inventory/PROD-001/set-stock \
  -H "Content-Type: application/json" \
  -d '{
    "newQuantity": 100,
    "reason": "Initial stock",
    "changedBy": "admin"
  }'
```

### Get Stock (Inventory)
```bash
curl http://localhost:5003/api/inventory/PROD-001/stock
```

---

## Docker Compose Configuration

The `docker-compose.yml` file includes:

- **MongoDB**: Persistence layer with authentication
- **RabbitMQ**: Message broker with management console
- **8 Microservices**: 4 APIs + 4 Background workers

All services are connected through a custom bridge network (`ecommerce-network`).

Health checks ensure services are ready before dependents start.

---

## Troubleshooting

### Port Already in Use
If you get "port already in use" error:

```bash
# Find what's using the port (macOS/Linux)
lsof -i :5000

# Kill the process
kill -9 <PID>
```

### Services Won't Start
Check logs:
```bash
# Docker Compose
docker-compose logs [service_name]

# Shell script
tail -f /tmp/[service_name].log
```

### MongoDB Connection Issues
Verify MongoDB is running:
```bash
docker-compose logs mongodb
```

Reset MongoDB data:
```bash
docker-compose down -v
docker-compose up -d
```

### RabbitMQ Issues
Access management console: http://localhost:15672
- Username: guest
- Password: guest

---

## Development Workflow

### Using Docker Compose + Local Services (Best for Development)

1. Start only infrastructure:
   ```bash
   ./run-all.sh --docker-only
   ```

2. Start specific service in development mode:
   ```bash
   cd src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandApi
   dotnet run
   ```

3. Other services continue running in background from step 1

---

## Production Notes

For production deployment:
- Use the Docker images built by the Dockerfiles
- Set appropriate environment variables for MongoDB and RabbitMQ
- Use container orchestration (Kubernetes, Docker Swarm, etc.)
- Implement proper networking and security policies
- Use secret management for credentials
- Monitor with appropriate logging and tracing infrastructure

---

## Files

- `run-all.sh` - Master startup script
- `docker-compose.yml` - Multi-service Docker configuration
- `src/*/Dockerfile` - Individual service Docker images
- `.dockerignore` - Files to exclude from Docker builds
