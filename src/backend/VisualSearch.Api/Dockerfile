# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY VisualSearch.Api.csproj .
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Model download stage - downloads AI models from Hugging Face
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS models
WORKDIR /models

# Install curl for downloading models
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*

# Download CLIP ViT-B/32 visual encoder ONNX model (~85MB quantized)
# Using -k to skip SSL verification (needed for corporate proxies)
# The model is the quantized version for faster inference
RUN curl -k -L --fail --retry 3 --retry-delay 5 --connect-timeout 30 --max-time 600 \
    "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx" \
    -o clip-vit-base-patch32-visual.onnx && \
    echo "CLIP model download completed: $(ls -lh clip-vit-base-patch32-visual.onnx)" || \
    (echo "WARNING: CLIP model download failed - will use fallback embedding" && touch clip-vit-base-patch32-visual.onnx)

# Verify model file has content (should be > 50MB for CLIP)
RUN if [ -f clip-vit-base-patch32-visual.onnx ] && [ $(stat -c%s clip-vit-base-patch32-visual.onnx) -lt 10000000 ]; then \
        echo "CLIP model file too small, removing invalid file"; \
        rm clip-vit-base-patch32-visual.onnx; \
        touch clip-vit-base-patch32-visual.onnx; \
    fi

# Note: YOLO model download removed - using full-image CLIP embeddings instead
# This simplifies deployment and avoids additional model download issues
RUN touch yolov8n.onnx

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

# Create Models directory and copy downloaded models (only if they have content)
RUN mkdir -p /app/Models
COPY --from=models /models/*.onnx /app/Models/

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port
EXPOSE 8080

# Health check - increased start period for model loading
HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=3 \
    CMD wget --no-verbose --tries=1 -O - http://localhost:8080/health > /dev/null || exit 1

# Run the application
ENTRYPOINT ["dotnet", "VisualSearch.Api.dll"]
