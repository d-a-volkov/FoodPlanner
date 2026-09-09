# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FoodPlanner.Core/FoodPlanner.Core.csproj FoodPlanner.Core/
COPY FoodPlanner.Data/FoodPlanner.Data.csproj FoodPlanner.Data/
COPY FoodPlanner.Services/FoodPlanner.Services.csproj FoodPlanner.Services/
COPY FoodPlanner.Bot.Telegram/FoodPlanner.Bot.Telegram.csproj FoodPlanner.Bot.Telegram/
RUN dotnet restore FoodPlanner.Bot.Telegram/FoodPlanner.Bot.Telegram.csproj
COPY . .
RUN dotnet publish FoodPlanner.Bot.Telegram/FoodPlanner.Bot.Telegram.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FoodPlanner.Bot.Telegram.dll"]
