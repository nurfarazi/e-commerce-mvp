# Multi-stage build for ProductCatalog CommandHandler
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file
COPY ["ECommerceMvp.sln", "."]

# Copy project files
COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "src/Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Shared/ECommerceMvp.Shared.Application/ECommerceMvp.Shared.Application.csproj", "src/Shared/ECommerceMvp.Shared.Application/"]
COPY ["src/Shared/ECommerceMvp.Shared.Infrastructure/ECommerceMvp.Shared.Infrastructure.csproj", "src/Shared/ECommerceMvp.Shared.Infrastructure/"]
COPY ["src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/ECommerceMvp.ProductCatalog.Domain.csproj", "src/ProductCatalog/ECommerceMvp.ProductCatalog.Domain/"]
COPY ["src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/ECommerceMvp.ProductCatalog.Application.csproj", "src/ProductCatalog/ECommerceMvp.ProductCatalog.Application/"]
COPY ["src/ProductCatalog/ECommerceMvp.ProductCatalog.Infrastructure/ECommerceMvp.ProductCatalog.Infrastructure.csproj", "src/ProductCatalog/ECommerceMvp.ProductCatalog.Infrastructure/"]
COPY ["src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler/ECommerceMvp.ProductCatalog.CommandHandler.csproj", "src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler/"]

# Restore dependencies
RUN dotnet restore "ECommerceMvp.sln"

# Copy full source
COPY . .

# Build
RUN dotnet build "ECommerceMvp.sln" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/ProductCatalog/ECommerceMvp.ProductCatalog.CommandHandler/ECommerceMvp.ProductCatalog.CommandHandler.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.ProductCatalog.CommandHandler.dll"]
