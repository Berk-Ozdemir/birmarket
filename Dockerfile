FROM node:24-alpine AS web-build
WORKDIR /src/frontend/birmarket-web
COPY frontend/birmarket-web/package.json frontend/birmarket-web/package-lock.json ./
RUN npm ci
COPY frontend/birmarket-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY backend/Birmarket.Api/Birmarket.Api.csproj backend/Birmarket.Api/
RUN dotnet restore backend/Birmarket.Api/Birmarket.Api.csproj
COPY backend/Birmarket.Api/ backend/Birmarket.Api/
RUN dotnet publish backend/Birmarket.Api/Birmarket.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install --yes --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api-build /app/publish/ ./
COPY --from=web-build /src/frontend/birmarket-web/dist/birmarket-web/browser/ ./wwwroot/
COPY birmarket.env.example ./
RUN mkdir -p /app/.data && chown -R $APP_UID:$APP_UID /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:8080/api/health/ready || exit 1
USER $APP_UID
ENTRYPOINT ["dotnet", "Birmarket.Api.dll"]
