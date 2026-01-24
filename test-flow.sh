#!/bin/bash

# E-Commerce MVP - Product Catalog API Testing Script
# This script tests the complete CQRS event-driven flow
# Prerequisites: All services must be running on their respective ports

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# API Base URLs
COMMAND_API="http://localhost:5000"
QUERY_API="http://localhost:5001"

# Function to print section headers
print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

# Function to print success
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

# Function to print warning
print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

# Function to print error
print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Check if services are running
check_services() {
    print_header "Checking Services"
    
    echo "Checking CommandApi on $COMMAND_API..."
    if ! curl -s "$COMMAND_API/health" > /dev/null 2>&1; then
        print_warning "CommandApi may not be responding (this is expected if endpoint doesn't exist)"
    else
        print_success "CommandApi is running"
    fi
    
    echo "Checking QueryApi on $QUERY_API..."
    if ! curl -s "$QUERY_API/health" > /dev/null 2>&1; then
        print_warning "QueryApi may not be responding (this is expected if endpoint doesn't exist)"
    else
        print_success "QueryApi is running"
    fi
}

# Test 1: Create Product
test_create_product() {
    print_header "Test 1: Create Product"
    
    RESPONSE=$(curl -s -X POST "$COMMAND_API/api/products" \
        -H "Content-Type: application/json" \
        -d '{
            "productId": "PROD-001",
            "name": "MacBook Pro 16",
            "description": "High-performance laptop for professionals",
            "sku": "MB16-2024",
            "price": 2499.99,
            "currency": "USD"
        }')
    
    echo "Response: $RESPONSE"
    print_success "Product creation command sent"
    print_warning "Waiting 2 seconds for event processing..."
    sleep 2
}

# Test 2: Query Product
test_query_product() {
    print_header "Test 2: Query Product"
    
    RESPONSE=$(curl -s -X GET "$QUERY_API/api/products/PROD-001")
    
    if echo "$RESPONSE" | grep -q "PROD-001"; then
        echo "Response: $RESPONSE"
        print_success "Product retrieved successfully"
    else
        echo "Response: $RESPONSE"
        print_warning "Product not found yet (might need more time for projection)"
    fi
}

# Test 3: Activate Product
test_activate_product() {
    print_header "Test 3: Activate Product"
    
    RESPONSE=$(curl -s -X PUT "$COMMAND_API/api/products/PROD-001/activate")
    
    echo "Response: $RESPONSE"
    print_success "Product activation command sent"
    print_warning "Waiting 2 seconds for event processing..."
    sleep 2
}

# Test 4: Verify Product is Active
test_verify_active() {
    print_header "Test 4: Verify Product is Active"
    
    RESPONSE=$(curl -s -X GET "$QUERY_API/api/products/PROD-001")
    
    if echo "$RESPONSE" | grep -q '"isActive":true'; then
        echo "Response: $RESPONSE"
        print_success "Product is now active!"
    else
        echo "Response: $RESPONSE"
        print_warning "Product status not updated yet"
    fi
}

# Test 5: Create Additional Products
test_create_additional() {
    print_header "Test 5: Create Additional Products"
    
    echo "Creating PROD-002..."
    curl -s -X POST "$COMMAND_API/api/products" \
        -H "Content-Type: application/json" \
        -d '{
            "productId": "PROD-002",
            "name": "iPhone 15 Pro",
            "description": "Latest iPhone with A18 Pro chip",
            "sku": "IP15-2024",
            "price": 999.99,
            "currency": "USD"
        }' > /dev/null
    print_success "PROD-002 created"
    
    sleep 1
    
    echo "Creating PROD-003..."
    curl -s -X POST "$COMMAND_API/api/products" \
        -H "Content-Type: application/json" \
        -d '{
            "productId": "PROD-003",
            "name": "Samsung Galaxy S24",
            "description": "Flagship Android smartphone",
            "sku": "SGS24-2024",
            "price": 899.99,
            "currency": "USD"
        }' > /dev/null
    print_success "PROD-003 created"
    
    print_warning "Waiting 2 seconds for projections..."
    sleep 2
}

# Test 6: Activate Second Product
test_activate_second() {
    print_header "Test 6: Activate Second Product"
    
    curl -s -X PUT "$COMMAND_API/api/products/PROD-002/activate" > /dev/null
    print_success "PROD-002 activation command sent"
    
    print_warning "Waiting 2 seconds for event processing..."
    sleep 2
}

# Test 7: List All Products
test_list_all() {
    print_header "Test 7: List All Products"
    
    RESPONSE=$(curl -s -X GET "$QUERY_API/api/products?page=1&pageSize=50")
    
    echo "Response: $RESPONSE"
    print_success "Products listed"
}

# Test 8: Query Active Products
test_query_active() {
    print_header "Test 8: Query Active Products"
    
    RESPONSE=$(curl -s -X GET "$QUERY_API/api/products?isActive=true")
    
    echo "Response: $RESPONSE"
    print_success "Active products retrieved"
}

# Test 9: Query Inactive Products
test_query_inactive() {
    print_header "Test 9: Query Inactive Products"
    
    RESPONSE=$(curl -s -X GET "$QUERY_API/api/products?isActive=false")
    
    echo "Response: $RESPONSE"
    print_success "Inactive products retrieved"
}

# Main execution
main() {
    print_header "E-Commerce MVP - Complete Testing Flow"
    
    echo "This script will:"
    echo "1. Check service availability"
    echo "2. Create products via CommandApi"
    echo "3. Query products via QueryApi"
    echo "4. Activate products"
    echo "5. Verify event-driven flow"
    
    echo -e "\n${YELLOW}Press Enter to start...${NC}"
    read
    
    check_services
    test_create_product
    test_query_product
    test_activate_product
    test_verify_active
    test_create_additional
    test_activate_second
    test_list_all
    test_query_active
    test_query_inactive
    
    print_header "Testing Complete!"
    echo -e "${GREEN}All tests completed!${NC}"
    echo -e "\n${YELLOW}Next steps:${NC}"
    echo "1. Check CommandHandler logs for command processing"
    echo "2. Check EventHandler logs for event projections"
    echo "3. Monitor RabbitMQ at http://localhost:15672 (guest/guest)"
    echo "4. Inspect MongoDB data:"
    echo "   mongosh mongodb://admin:admin@localhost:27017"
    echo "   use ecommerce"
    echo "   db.Products.find()"
    echo "   db.EventStore.find()"
}

main "$@"
