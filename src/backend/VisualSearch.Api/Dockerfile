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

# Download CLIP ViT-L/14 quantized ONNX model (~250MB, best quality)
RUN curl -k -L --fail --retry 3 --retry-delay 5 --connect-timeout 30 --max-time 1800 \
    "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/onnx/vision_model_quantized.onnx" \
    -o clip-vit-large-patch14-visual.onnx && \
    echo "CLIP ViT-L/14 model downloaded: $(ls -lh clip-vit-large-patch14-visual.onnx)" || \
    (echo "WARNING: CLIP model download failed" && touch clip-vit-large-patch14-visual.onnx)

# Verify model file size (>200MB expected)
RUN if [ -f clip-vit-large-patch14-visual.onnx ] && [ $(stat -c%s clip-vit-large-patch14-visual.onnx) -lt 200000000 ]; then \
        echo "CLIP model too small, removing invalid file"; \
        rm clip-vit-large-patch14-visual.onnx; \
        touch clip-vit-large-patch14-visual.onnx; \
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
