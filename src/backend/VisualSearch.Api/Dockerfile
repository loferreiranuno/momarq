# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY VisualSearch.Api.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install required native libraries for ONNX Runtime and wget for health checks
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create directory for ONNX models (optional, for server-side embedding)
RUN mkdir -p /app/Models

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port
EXPOSE 8080

# Health check (use GET because the endpoint does not allow HEAD)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 -O - http://localhost:8080/health > /dev/null || exit 1

# Run the application
ENTRYPOINT ["dotnet", "VisualSearch.Api.dll"]
