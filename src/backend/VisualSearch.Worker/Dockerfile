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

# Runtime stage - use Playwright base image for browser support
FROM mcr.microsoft.com/playwright/dotnet:v1.49.0-noble AS final
WORKDIR /app

# Install .NET 9.0 runtime (Playwright image only has .NET 8.0)
RUN apt-get update && apt-get install -y wget \
    && wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Install only Chromium browser (smaller footprint than all browsers)
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN pwsh -Command "& { (Get-Content -Path /ms-playwright-agent/package.json | ConvertFrom-Json).version }" || true

# The Playwright base image already has a non-root user (pwuser with UID 1001)
# We'll use that user for security instead of creating a new one

# Copy published app
COPY --from=publish /app/publish .

# Change ownership of app directory to pwuser (Playwright's default user)
RUN chown -R pwuser:pwuser /app

USER pwuser

# Health check - worker should respond to SIGTERM gracefully
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD pgrep -f "dotnet.*VisualSearch.Worker" || exit 1

ENTRYPOINT ["dotnet", "VisualSearch.Worker.dll"]
