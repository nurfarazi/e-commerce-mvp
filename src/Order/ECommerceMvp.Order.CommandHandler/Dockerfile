FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Shared/ECommerceMvp.Shared.Domain/ECommerceMvp.Shared.Domain.csproj", "src/Shared/ECommerceMvp.Shared.Domain/"]
COPY ["src/Shared/ECommerceMvp.Shared.Application/ECommerceMvp.Shared.Application.csproj", "src/Shared/ECommerceMvp.Shared.Application/"]
COPY ["src/Shared/ECommerceMvp.Shared.Infrastructure/ECommerceMvp.Shared.Infrastructure.csproj", "src/Shared/ECommerceMvp.Shared.Infrastructure/"]
COPY ["src/Order/ECommerceMvp.Order.Domain/ECommerceMvp.Order.Domain.csproj", "src/Order/ECommerceMvp.Order.Domain/"]
COPY ["src/Order/ECommerceMvp.Order.Application/ECommerceMvp.Order.Application.csproj", "src/Order/ECommerceMvp.Order.Application/"]
COPY ["src/Order/ECommerceMvp.Order.Infrastructure/ECommerceMvp.Order.Infrastructure.csproj", "src/Order/ECommerceMvp.Order.Infrastructure/"]
COPY ["src/Order/ECommerceMvp.Order.CommandHandler/ECommerceMvp.Order.CommandHandler.csproj", "src/Order/ECommerceMvp.Order.CommandHandler/"]

RUN dotnet restore "src/Order/ECommerceMvp.Order.CommandHandler/ECommerceMvp.Order.CommandHandler.csproj"

COPY . .
WORKDIR "/src/src/Order/ECommerceMvp.Order.CommandHandler"
RUN dotnet build "ECommerceMvp.Order.CommandHandler.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECommerceMvp.Order.CommandHandler.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceMvp.Order.CommandHandler.dll"]
