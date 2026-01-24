FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "src/Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Order/ECommerceMvp.Order.Domain/ECommerceMvp.Order.Domain.csproj", "src/Order/ECommerceMvp.Order.Domain/"]
COPY ["src/Order/ECommerceMvp.Order.QueryApi/ECommerceMvp.Order.QueryApi.csproj", "src/Order/ECommerceMvp.Order.QueryApi/"]
COPY ["src/Order/ECommerceMvp.Order.QueryApiServer/ECommerceMvp.Order.QueryApiServer.csproj", "src/Order/ECommerceMvp.Order.QueryApiServer/"]

RUN dotnet restore "src/Order/ECommerceMvp.Order.QueryApiServer/ECommerceMvp.Order.QueryApiServer.csproj"

COPY . .
WORKDIR "/src/src/Order/ECommerceMvp.Order.QueryApiServer"
RUN dotnet build "ECommerceMvp.Order.QueryApiServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECommerceMvp.Order.QueryApiServer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.Order.QueryApiServer.dll"]
