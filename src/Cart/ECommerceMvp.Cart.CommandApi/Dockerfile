FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Cart/ECommerceMvp.Cart.CommandApi/ECommerceMvp.Cart.CommandApi.csproj", "Cart/ECommerceMvp.Cart.CommandApi/"]
COPY ["src/Cart/ECommerceMvp.Cart.Application/ECommerceMvp.Cart.Application.csproj", "Cart/ECommerceMvp.Cart.Application/"]
COPY ["src/Cart/ECommerceMvp.Cart.Domain/ECommerceMvp.Cart.Domain.csproj", "Cart/ECommerceMvp.Cart.Domain/"]
COPY ["src/Shared/ECommerceMvp.Shared.Application/ECommerceMvp.Shared.Application.csproj", "Shared/ECommerceMvp.Shared.Application/"]
COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Shared/ECommerceMvp.Shared.Infrastructure/ECommerceMvp.Shared.Infrastructure.csproj", "Shared/ECommerceMvp.Shared.Infrastructure/"]

RUN dotnet restore "Cart/ECommerceMvp.Cart.CommandApi/ECommerceMvp.Cart.CommandApi.csproj"

COPY . .
WORKDIR "/src/src/Cart/ECommerceMvp.Cart.CommandApi"
RUN dotnet build "ECommerceMvp.Cart.CommandApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECommerceMvp.Cart.CommandApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.Cart.CommandApi.dll"]
