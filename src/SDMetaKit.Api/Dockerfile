# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# cache warstwa — restore
COPY nuget.config ./
COPY SDMetaKit.slnx ./
COPY src/SDMetaKit.Core/SDMetaKit.Core.csproj src/SDMetaKit.Core/
COPY src/SDMetaKit.Api/SDMetaKit.Api.csproj src/SDMetaKit.Api/
RUN dotnet restore src/SDMetaKit.Api/SDMetaKit.Api.csproj

# build + publish
COPY . .
WORKDIR /src/src/SDMetaKit.Api
RUN dotnet restore && dotnet publish -c Release -o /app

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PathBase=/sdmk

# curl dla HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "SDMetaKit.Api.dll"]
