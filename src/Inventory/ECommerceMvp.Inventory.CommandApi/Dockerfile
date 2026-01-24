# Multi-stage build for Inventory CommandApi
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
COPY ["src/Inventory/ECommerceMvp.Inventory.CommandApi/ECommerceMvp.Inventory.CommandApi.csproj", "src/Inventory/ECommerceMvp.Inventory.CommandApi/"]

# Restore dependencies
RUN dotnet restore "src/Inventory/ECommerceMvp.Inventory.CommandApi/ECommerceMvp.Inventory.CommandApi.csproj"

# Copy full source
COPY . .

# Build
RUN dotnet build "src/Inventory/ECommerceMvp.Inventory.CommandApi/ECommerceMvp.Inventory.CommandApi.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/Inventory/ECommerceMvp.Inventory.CommandApi/ECommerceMvp.Inventory.CommandApi.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ECommerceMvp.Inventory.CommandApi.dll"]
