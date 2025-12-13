# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/backend/VisualSearch.Worker/VisualSearch.Worker.csproj", "src/backend/VisualSearch.Worker/"]
COPY ["src/backend/VisualSearch.Contracts/VisualSearch.Contracts.csproj", "src/backend/VisualSearch.Contracts/"]
RUN dotnet restore "src/backend/VisualSearch.Worker/VisualSearch.Worker.csproj"

# Copy source code and build
COPY src/backend/VisualSearch.Worker/ src/backend/VisualSearch.Worker/
COPY src/backend/VisualSearch.Contracts/ src/backend/VisualSearch.Contracts/
WORKDIR /src/src/backend/VisualSearch.Worker
RUN dotnet build "VisualSearch.Worker.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "VisualSearch.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" --uid 1001 worker
USER worker

COPY --from=publish /app/publish .

# Health check - worker should respond to SIGTERM gracefully
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD pgrep -f "dotnet.*VisualSearch.Worker" || exit 1

ENTRYPOINT ["dotnet", "VisualSearch.Worker.dll"]
