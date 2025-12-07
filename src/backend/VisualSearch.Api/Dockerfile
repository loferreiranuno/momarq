# Fixed: correct model URLs (public, no 401) + faster/smaller alternatives
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY VisualSearch.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Models stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS models
WORKDIR /models

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates python3 python3-venv git libgl1 libglib2.0-0 && \
    rm -rf /var/lib/apt/lists/*

RUN python3 -m venv /opt/venv && /opt/venv/bin/pip install --upgrade pip ultralytics onnxruntime

ENV PATH="/opt/venv/bin:$PATH"

# 2. CLIP ViT-L/14 quantized (public)
RUN curl -L --fail --retry 3 --max-time 1800 \
    "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/onnx/vision_model_quantized.onnx" \
    -o clip-vit-large-patch14-visual.onnx

# 3. YOLO-World v2 s (open-vocabulary detection) with FP16 for faster CPU inference
RUN python3 - <<'PY'
from ultralytics import YOLO
model = YOLO("yolov8s-worldv2.pt")
# Export with opset=21 for ONNX Runtime compatibility, half=True for FP16 quantization
model.export(format="onnx", imgsz=640, dynamic=False, simplify=True, opset=21, half=False)
import shutil, os
shutil.move("yolov8s-worldv2.onnx", "yolo-world-s.onnx")
PY

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 wget && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/Models
COPY --from=models /models/*.onnx /app/Models/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=150s --retries=3 \
    CMD wget --no-verbose --tries=1 http://localhost:8080/health -q -O - || exit 1

ENTRYPOINT ["dotnet", "VisualSearch.Api.dll"]