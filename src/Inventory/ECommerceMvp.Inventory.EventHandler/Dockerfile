# Multi-stage build for Inventory EventHandler
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ["ECommerceMvp.sln", "."]

# Copy project files
COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "src/Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Shared/ECommerceMvp.Shared.Application/ECommerceMvp.Shared.Application.csproj", "src/Shared/ECommerceMvp.Shared.Application/"]
COPY ["src/Shared/ECommerceMvp.Shared.Infrastructure/ECommerceMvp.Shared.Infrastructure.csproj", "src/Shared/ECommerceMvp.Shared.Infrastructure/"]
COPY ["src/Inventory/ECommerceMvp.Inventory.Domain/ECommerceMvp.Inventory.Domain.csproj", "src/Inventory/ECommerceMvp.Inventory.Domain/"]
COPY ["src/Inventory/ECommerceMvp.Inventory.Application/ECommerceMvp.Inventory.Application.csproj", "src/Inventory/ECommerceMvp.Inventory.Application/"]
COPY ["src/Inventory/ECommerceMvp.Inventory.Infrastructure/ECommerceMvp.Inventory.Infrastructure.csproj", "src/Inventory/ECommerceMvp.Inventory.Infrastructure/"]
COPY ["src/Inventory/ECommerceMvp.Inventory.EventHandler/ECommerceMvp.Inventory.EventHandler.csproj", "src/Inventory/ECommerceMvp.Inventory.EventHandler/"]

# Restore dependencies
RUN dotnet restore "src/Inventory/ECommerceMvp.Inventory.EventHandler/ECommerceMvp.Inventory.EventHandler.csproj"

# Copy full source
COPY . .

# Build
RUN dotnet build "src/Inventory/ECommerceMvp.Inventory.EventHandler/ECommerceMvp.Inventory.EventHandler.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/Inventory/ECommerceMvp.Inventory.EventHandler/ECommerceMvp.Inventory.EventHandler.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.Inventory.EventHandler.dll"]
