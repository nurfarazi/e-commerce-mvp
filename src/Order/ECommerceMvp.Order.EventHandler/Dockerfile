FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "src/Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Order/ECommerceMvp.Order.Domain/ECommerceMvp.Order.Domain.csproj", "src/Order/ECommerceMvp.Order.Domain/"]
COPY ["src/Order/ECommerceMvp.Order.QueryApi/ECommerceMvp.Order.QueryApi.csproj", "src/Order/ECommerceMvp.Order.QueryApi/"]
COPY ["src/Order/ECommerceMvp.Order.EventHandler/ECommerceMvp.Order.EventHandler.csproj", "src/Order/ECommerceMvp.Order.EventHandler/"]

RUN dotnet restore "src/Order/ECommerceMvp.Order.EventHandler/ECommerceMvp.Order.EventHandler.csproj"

COPY . .
WORKDIR "/src/src/Order/ECommerceMvp.Order.EventHandler"
RUN dotnet build "ECommerceMvp.Order.EventHandler.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECommerceMvp.Order.EventHandler.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.Order.EventHandler.dll"]
