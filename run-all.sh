#!/bin/bash

# E-Commerce MVP - Complete Startup Script
# This script builds, starts Docker services, and runs all microservices
# Usage: ./run-all.sh [--docker-only] [--build-only] [--clean]

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR"

# Default ports
MONGO_PORT=27017
RABBITMQ_PORT=5672
INVENTORY_CMD_API_PORT=5002
INVENTORY_QUERY_API_PORT=5003
INVENTORY_CMD_HANDLER_PORT=9001
INVENTORY_EVENT_HANDLER_PORT=9002
PRODUCTCATALOG_CMD_API_PORT=5000
PRODUCTCATALOG_QUERY_API_PORT=5001
PRODUCTCATALOG_CMD_HANDLER_PORT=9003
PRODUCTCATALOG_EVENT_HANDLER_PORT=9004

# Parse arguments
DOCKER_ONLY=false
BUILD_ONLY=false
CLEAN=false

for arg in "$@"; do
    case $arg in
        --docker-only)
            DOCKER_ONLY=true
            shift
            ;;
        --build-only)
            BUILD_ONLY=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        *)
            shift
            ;;
    esac
done

# Print header
print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

# Clean build artifacts
if [ "$CLEAN" = true ]; then
    print_header "Cleaning Build Artifacts"
    print_info "Removing bin and obj directories..."
    cd "$PROJECT_DIR"
    rm -rf bin obj src/**/bin src/**/obj tests/**/bin tests/**/obj
    print_success "Cleaned"
fi

# Build solution
if [ "$BUILD_ONLY" = false ] || [ "$BUILD_ONLY" = true ]; then
    print_header "Building Solution"
    cd "$PROJECT_DIR"
    
    if ! dotnet build; then
        print_error "Build failed"
        exit 1
    fi
    print_success "Build completed"
fi

# If build-only flag, exit here
if [ "$BUILD_ONLY" = true ]; then
    exit 0
fi

# Start Docker services
print_header "Starting Docker Services"

if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed"
    exit 1
fi

cd "$PROJECT_DIR"

if ! docker-compose up -d; then
    print_error "Failed to start Docker services"
    exit 1
fi

print_success "Docker services started"

# Wait for services to be healthy
print_header "Waiting for Services to be Ready"

wait_for_service() {
    local service_name=$1
    local port=$2
    local max_attempts=30
    local attempt=0
    
    echo "Waiting for $service_name on port $port..."
    
    while [ $attempt -lt $max_attempts ]; do
        if nc -z localhost $port 2>/dev/null; then
            print_success "$service_name is ready"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 1
    done
    
    print_error "$service_name did not become ready"
    return 1
}

wait_for_service "MongoDB" $MONGO_PORT
wait_for_service "RabbitMQ" $RABBITMQ_PORT

# If docker-only flag, exit here
if [ "$DOCKER_ONLY" = true ]; then
    print_header "Setup Complete"
    print_info "Docker services are running:"
    print_info "  MongoDB: localhost:$MONGO_PORT"
    print_info "  RabbitMQ: localhost:$RABBITMQ_PORT (Management: localhost:15672)"
    exit 0
fi

# Start .NET services
print_header "Starting Microservices"

# Function to start a service in background
start_service() {
    local project_path=$1
    local service_name=$2
    local port=$3
    
    print_info "Starting $service_name on port $port..."
    
    cd "$project_path"
    dotnet run --no-build > "/tmp/${service_name}.log" 2>&1 &
    local pid=$!
    echo $pid > "/tmp/${service_name}.pid"
    
    sleep 2
    
    if ! kill -0 $pid 2>/dev/null; then
        print_error "$service_name failed to start"
        cat "/tmp/${service_name}.log"
        return 1
    fi
    
    print_success "$service_name started (PID: $pid)"
    return 0
}

# Start all services
print_info "Starting ProductCatalog CommandApi..."
start_service "$PROJECT_DIR/src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandApi" \
    "ProductCatalog-CommandApi" $PRODUCTCATALOG_CMD_API_PORT

print_info "Starting ProductCatalog QueryApi..."
start_service "$PROJECT_DIR/src/ProductCatalog/ECommerceMvp.ProductCatalog.QueryApi" \
    "ProductCatalog-QueryApi" $PRODUCTCATALOG_QUERY_API_PORT

print_info "Starting Inventory CommandApi..."
start_service "$PROJECT_DIR/src/Inventory/ECommerceMvp.Inventory.CommandApi" \
    "Inventory-CommandApi" $INVENTORY_CMD_API_PORT

print_info "Starting Inventory QueryApi..."
start_service "$PROJECT_DIR/src/Inventory/ECommerceMvp.Inventory.QueryApi" \
    "Inventory-QueryApi" $INVENTORY_QUERY_API_PORT

print_info "Starting ProductCatalog CommandHandler..."
start_service "$PROJECT_DIR/src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler" \
    "ProductCatalog-CommandHandler" $PRODUCTCATALOG_CMD_HANDLER_PORT

print_info "Starting ProductCatalog EventHandler..."
start_service "$PROJECT_DIR/src/ProductCatalog/ECommerceMvp.ProductCatalog.EventHandler" \
    "ProductCatalog-EventHandler" $PRODUCTCATALOG_EVENT_HANDLER_PORT

print_info "Starting Inventory CommandHandler..."
start_service "$PROJECT_DIR/src/Inventory/ECommerceMvp.Inventory.CommandHandler" \
    "Inventory-CommandHandler" $INVENTORY_CMD_HANDLER_PORT

print_info "Starting Inventory EventHandler..."
start_service "$PROJECT_DIR/src/Inventory/ECommerceMvp.Inventory.EventHandler" \
    "Inventory-EventHandler" $INVENTORY_EVENT_HANDLER_PORT

# Print summary
print_header "All Services Running"

echo -e "${GREEN}Services:${NC}"
echo "  ProductCatalog CommandApi:  http://localhost:$PRODUCTCATALOG_CMD_API_PORT"
echo "  ProductCatalog QueryApi:    http://localhost:$PRODUCTCATALOG_QUERY_API_PORT"
echo "  Inventory CommandApi:       http://localhost:$INVENTORY_CMD_API_PORT"
echo "  Inventory QueryApi:         http://localhost:$INVENTORY_QUERY_API_PORT"
echo ""
echo -e "${GREEN}Infrastructure:${NC}"
echo "  MongoDB:                    mongodb://localhost:$MONGO_PORT (admin/admin)"
echo "  RabbitMQ:                   amqp://localhost:$RABBITMQ_PORT (guest/guest)"
echo "  RabbitMQ Management:        http://localhost:15672 (guest/guest)"
echo ""
echo -e "${YELLOW}Log files:${NC}"
echo "  /tmp/ProductCatalog-CommandApi.log"
echo "  /tmp/ProductCatalog-QueryApi.log"
echo "  /tmp/Inventory-CommandApi.log"
echo "  /tmp/Inventory-QueryApi.log"
echo "  /tmp/ProductCatalog-CommandHandler.log"
echo "  /tmp/ProductCatalog-EventHandler.log"
echo "  /tmp/Inventory-CommandHandler.log"
echo "  /tmp/Inventory-EventHandler.log"
echo ""

# Function to stop services
cleanup() {
    print_header "Shutting Down Services"
    
    for pid_file in /tmp/*-*.pid; do
        if [ -f "$pid_file" ]; then
            pid=$(cat "$pid_file")
            service_name=$(basename "$pid_file" .pid)
            if kill -0 $pid 2>/dev/null; then
                print_info "Stopping $service_name..."
                kill $pid
                rm "$pid_file"
            fi
        fi
    done
    
    print_info "Stopping Docker services..."
    cd "$PROJECT_DIR"
    docker-compose down
    
    print_success "All services stopped"
}

# Set trap to handle Ctrl+C
trap cleanup EXIT INT TERM

# Keep script running
print_info "Press Ctrl+C to stop all services"
wait
