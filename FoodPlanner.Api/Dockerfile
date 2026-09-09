# syntax=docker/dockerfile:1

#### Stage 1: build Vue web app ####
FROM node:22-alpine AS web
WORKDIR /src/web
COPY FoodPlanner.Web/package*.json ./
RUN npm ci
COPY FoodPlanner.Web/ ./
RUN npm run build

#### Stage 2: build & publish API ####
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FoodPlanner.Core/FoodPlanner.Core.csproj FoodPlanner.Core/
COPY FoodPlanner.Data/FoodPlanner.Data.csproj FoodPlanner.Data/
COPY FoodPlanner.Services/FoodPlanner.Services.csproj FoodPlanner.Services/
COPY FoodPlanner.Api/FoodPlanner.Api.csproj FoodPlanner.Api/
RUN dotnet restore FoodPlanner.Api/FoodPlanner.Api.csproj
COPY . .
RUN dotnet publish FoodPlanner.Api/FoodPlanner.Api.csproj -c Release -o /app/publish --no-restore

#### Stage 3: runtime ####
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web /src/web/dist /app/webdist
COPY FoodPlanner.Api/Data /app/seed
ENV ASPNETCORE_URLS=http://+:8080
ENV DataPath=/data
ENV FrontendPath=/app/webdist
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "FoodPlanner.Api.dll"]
