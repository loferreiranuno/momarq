using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Services;

/// <summary>
/// Service for detecting objects in images using YOLOv8 ONNX model.
/// Used to identify furniture items in room photos for targeted visual search.
/// </summary>
public sealed class ObjectDetectionService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly ILogger<ObjectDetectionService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly YoloSettings _settings;
    private readonly bool _isModelLoaded;
    private readonly object _sessionLock = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private HashSet<int>? _enabledClassIds;
    private DateTime _lastCacheRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    // YOLOv8 input size
    private const int InputSize = 640;

    // COCO class names
    private static readonly string[] CocoClasses =
    [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
        "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
        "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
        "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
        "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
        "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
        "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
        "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop",
        "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
        "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDetectionService"/> class.
    /// </summary>
    public ObjectDetectionService(
        ILogger<ObjectDetectionService> logger,
        IWebHostEnvironment environment,
        IServiceProvider serviceProvider,
        IOptions<ModelSettings> settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings.Value.Yolo;

        var modelPath = Path.Combine(environment.ContentRootPath, "Models", "yolo-world-s.onnx");

        if (File.Exists(modelPath))
        {
            try
            {
                // Optimized session options using configuration
                var interOpThreads = _settings.InterOpNumThreads == 0 
                    ? Environment.ProcessorCount 
                    : _settings.InterOpNumThreads;
                var intraOpThreads = _settings.IntraOpNumThreads == 0 
                    ? Environment.ProcessorCount 
                    : _settings.IntraOpNumThreads;

                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    InterOpNumThreads = interOpThreads,
                    IntraOpNumThreads = intraOpThreads,
                    EnableMemoryPattern = _settings.EnableMemoryPattern,
                    EnableCpuMemArena = _settings.EnableCpuMemArena
                };

                _logger.LogInformation(
                    "YOLO ONNX session options: InterOp={InterOp}, IntraOp={IntraOp}, MemPattern={MemPattern}, CpuArena={CpuArena}",
                    interOpThreads, intraOpThreads, _settings.EnableMemoryPattern, _settings.EnableCpuMemArena);

                _session = new InferenceSession(modelPath, sessionOptions);
                _isModelLoaded = true;
                _logger.LogInformation("YOLO model loaded successfully from {ModelPath}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load YOLO model from {ModelPath}. Object detection will be disabled.", modelPath);
                _isModelLoaded = false;
            }
        }
        else
        {
            _logger.LogWarning("YOLO model not found at {ModelPath}. Object detection will be disabled.", modelPath);
            _isModelLoaded = false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the YOLO model is loaded and available.
    /// </summary>
    public bool IsModelLoaded => _isModelLoaded;

    /// <summary>
    /// Refreshes the cache of enabled category class IDs from the database asynchronously.
    /// </summary>
    public async Task RefreshEnabledCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                // Create a scope to get the scoped repository
                using var scope = _serviceProvider.CreateScope();
                var categoryRepository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
                
                var enabledCategories = await categoryRepository.GetEnabledForDetectionAsync(cancellationToken);
                _enabledClassIds = enabledCategories.Select(c => c.CocoClassId).ToHashSet();
                _lastCacheRefresh = DateTime.UtcNow;
                _logger.LogInformation("Refreshed enabled categories cache with {Count} class IDs", _enabledClassIds.Count);
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh enabled categories from database. Using cached values.");
        }
    }

    /// <summary>
    /// Gets the enabled class IDs, refreshing the cache if needed.
    /// </summary>
    private async Task<HashSet<int>> GetEnabledClassIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_enabledClassIds is null || DateTime.UtcNow - _lastCacheRefresh > CacheExpiration)
        {
            await RefreshEnabledCategoriesAsync(cancellationToken);
        }

        return _enabledClassIds ?? [];
    }

    /// <summary>
    /// Detects objects in an image and returns bounding boxes for furniture items.
    /// </summary>
    /// <param name="imageBytes">The raw image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of detected objects with their bounding boxes and class names.</returns>
    public async Task<List<DetectedObject>> DetectObjectsAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        return await DetectObjectsAsync(image, cancellationToken);
    }

    /// <summary>
    /// Detects objects in an image and returns bounding boxes for furniture items.
    /// </summary>
    /// <param name="image">The image to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of detected objects with their bounding boxes and class names.</returns>
    public async Task<List<DetectedObject>> DetectObjectsAsync(Image<Rgb24> image, CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded || _session is null)
        {
            _logger.LogDebug("YOLO model not loaded, returning empty detections.");
            return [];
        }

        // Get enabled class IDs from database (cached) - async to avoid blocking
        var enabledClassIds = await GetEnabledClassIdsAsync(cancellationToken);
        
        if (enabledClassIds.Count == 0)
        {
            _logger.LogWarning("No enabled categories found. Object detection will not filter any classes.");
        }

        try
        {
            // Pass image directly to preprocessing - avoid clone since we resize a copy internally
            return await Task.Run(() =>
            {
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                // Preprocess image for YOLO with parallel pixel processing
                var tensor = PreprocessImageParallel(image);

                // Run inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", tensor)
                };

                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
                lock (_sessionLock)
                {
                    results = _session!.Run(inputs);
                }

                using (results)
                {
                    var output = results.First().AsTensor<float>();

                    // Parse detections using configurable threshold
                    var detections = ParseDetections(output, originalWidth, originalHeight, _settings.ConfidenceThreshold);

                    // Apply NMS with configurable IoU threshold
                    var nmsDetections = ApplyNms(detections, _settings.IouThreshold);

                    // Filter to enabled furniture classes only (from database)
                    var furnitureDetections = enabledClassIds.Count > 0
                        ? nmsDetections.Where(d => enabledClassIds.Contains(d.ClassId)).ToList()
                        : nmsDetections;

                    _logger.LogInformation("Detected {Count} furniture objects in image", furnitureDetections.Count);
                    return furnitureDetections;
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect objects in image.");
            return [];
        }
    }

    /// <summary>
    /// Crops detected regions from an image.
    /// </summary>
    /// <param name="imageBytes">The original image bytes.</param>
    /// <param name="detections">The detected objects.</param>
    /// <param name="padding">Padding to add around each crop (as a fraction of box size).</param>
    /// <returns>A list of cropped image byte arrays.</returns>
    public List<byte[]> CropDetections(byte[] imageBytes, List<DetectedObject> detections, float padding = 0.1f)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        var crops = CropDetections(image, detections, padding);
        
        var result = new List<byte[]>();
        foreach (var crop in crops)
        {
            using (crop)
            using (var ms = new MemoryStream())
            {
                crop.SaveAsJpeg(ms);
                result.Add(ms.ToArray());
            }
        }
        return result;
    }

    /// <summary>
    /// Crops detected regions from an image.
    /// </summary>
    /// <param name="image">The original image.</param>
    /// <param name="detections">The detected objects.</param>
    /// <param name="padding">Padding to add around each crop (as a fraction of box size).</param>
    /// <returns>A list of cropped images.</returns>
    public List<Image<Rgb24>> CropDetections(Image<Rgb24> image, List<DetectedObject> detections, float padding = 0.1f)
    {
        var crops = new List<Image<Rgb24>>();

        foreach (var detection in detections)
        {
            try
            {
                // Calculate padded bounding box
                var boxWidth = detection.X2 - detection.X1;
                var boxHeight = detection.Y2 - detection.Y1;
                var padX = (int)(boxWidth * padding);
                var padY = (int)(boxHeight * padding);

                var x1 = Math.Max(0, detection.X1 - padX);
                var y1 = Math.Max(0, detection.Y1 - padY);
                var x2 = Math.Min(image.Width, detection.X2 + padX);
                var y2 = Math.Min(image.Height, detection.Y2 + padY);

                var cropWidth = x2 - x1;
                var cropHeight = y2 - y1;

                if (cropWidth <= 0 || cropHeight <= 0)
                {
                    continue;
                }

                // Clone and crop
                var crop = image.Clone(ctx => ctx.Crop(new Rectangle(x1, y1, cropWidth, cropHeight)));
                crops.Add(crop);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to crop detection: {ClassName}", detection.ClassName);
            }
        }

        return crops;
    }

    /// <summary>
    /// Preprocesses an image for YOLOv8 inference with parallel pixel processing.
    /// Creates a resized clone to avoid mutating the original image.
    /// </summary>
    private static DenseTensor<float> PreprocessImageParallel(Image<Rgb24> image)
    {
        // Resize to 640x640 with letterboxing - clone to avoid mutating original
        var scale = Math.Min((float)InputSize / image.Width, (float)InputSize / image.Height);
        var newWidth = (int)(image.Width * scale);
        var newHeight = (int)(image.Height * scale);

        using var resized = image.Clone(x => x.Resize(newWidth, newHeight));

        // Create tensor with padding (letterbox)
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);

        // Calculate offset for centering
        var offsetX = (InputSize - newWidth) / 2;
        var offsetY = (InputSize - newHeight) / 2;

        // Fill with gray (114/255) using parallel processing
        Parallel.For(0, InputSize, y =>
        {
            for (int c = 0; c < 3; c++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    tensor[0, c, y, x] = 114f / 255f;
                }
            }
        });

        // Copy image pixels with parallel row processing
        // First, extract pixel data to array for parallel access
        var pixelData = new Rgb24[newHeight * newWidth];
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    pixelData[y * newWidth + x] = row[x];
                }
            }
        });

        // Now process in parallel
        Parallel.For(0, newHeight, y =>
        {
            for (int x = 0; x < newWidth; x++)
            {
                var pixel = pixelData[y * newWidth + x];
                tensor[0, 0, y + offsetY, x + offsetX] = pixel.R / 255f;
                tensor[0, 1, y + offsetY, x + offsetX] = pixel.G / 255f;
                tensor[0, 2, y + offsetY, x + offsetX] = pixel.B / 255f;
            }
        });

        return tensor;
    }

    /// <summary>
    /// Parses YOLO output tensor into detection objects.
    /// </summary>
    /// <param name="output">The YOLO output tensor.</param>
    /// <param name="originalWidth">Original image width.</param>
    /// <param name="originalHeight">Original image height.</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold for detections.</param>
    /// <returns>List of detected objects.</returns>
    private List<DetectedObject> ParseDetections(Tensor<float> output, int originalWidth, int originalHeight, float confidenceThreshold)
    {
        var detections = new List<DetectedObject>();
        
        // YOLOv8 output shape: [1, 84, 8400] where 84 = 4 (bbox) + 80 (classes)
        var numDetections = output.Dimensions[2];
        var numClasses = output.Dimensions[1] - 4;

        // Calculate scale factors for letterbox
        var scale = Math.Min((float)InputSize / originalWidth, (float)InputSize / originalHeight);
        var offsetX = (InputSize - originalWidth * scale) / 2;
        var offsetY = (InputSize - originalHeight * scale) / 2;

        for (int i = 0; i < numDetections; i++)
        {
            // Get bbox coordinates (center x, center y, width, height)
            var cx = output[0, 0, i];
            var cy = output[0, 1, i];
            var w = output[0, 2, i];
            var h = output[0, 3, i];

            // Find best class
            var maxScore = 0f;
            var maxClassId = 0;
            for (int c = 0; c < numClasses; c++)
            {
                var score = output[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClassId = c;
                }
            }

            if (maxScore < confidenceThreshold)
            {
                continue;
            }

            // Convert to corner coordinates and scale back to original image
            var x1 = (cx - w / 2 - offsetX) / scale;
            var y1 = (cy - h / 2 - offsetY) / scale;
            var x2 = (cx + w / 2 - offsetX) / scale;
            var y2 = (cy + h / 2 - offsetY) / scale;

            // Clamp to image bounds
            x1 = Math.Max(0, Math.Min(originalWidth, x1));
            y1 = Math.Max(0, Math.Min(originalHeight, y1));
            x2 = Math.Max(0, Math.Min(originalWidth, x2));
            y2 = Math.Max(0, Math.Min(originalHeight, y2));

            detections.Add(new DetectedObject
            {
                ClassId = maxClassId,
                ClassName = maxClassId < CocoClasses.Length ? CocoClasses[maxClassId] : "unknown",
                Confidence = maxScore,
                X1 = (int)x1,
                Y1 = (int)y1,
                X2 = (int)x2,
                Y2 = (int)y2
            });
        }

        return detections;
    }

    /// <summary>
    /// Applies Non-Maximum Suppression to filter overlapping detections.
    /// </summary>
    /// <param name="detections">The list of detections to filter.</param>
    /// <param name="iouThreshold">The IoU threshold for filtering overlapping boxes.</param>
    /// <returns>Filtered list of detections.</returns>
    private static List<DetectedObject> ApplyNms(List<DetectedObject> detections, float iouThreshold)
    {
        var result = new List<DetectedObject>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = sorted.Where(d => CalculateIou(best, d) < iouThreshold).ToList();
        }

        return result;
    }

    /// <summary>
    /// Calculates Intersection over Union between two bounding boxes.
    /// </summary>
    private static float CalculateIou(DetectedObject a, DetectedObject b)
    {
        var x1 = Math.Max(a.X1, b.X1);
        var y1 = Math.Max(a.Y1, b.Y1);
        var x2 = Math.Min(a.X2, b.X2);
        var y2 = Math.Min(a.Y2, b.Y2);

        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
        var areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
        var union = areaA + areaB - intersection;

        return union > 0 ? intersection / union : 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _session?.Dispose();
        _cacheLock.Dispose();
    }
}

/// <summary>
/// Represents a detected object in an image.
/// </summary>
public sealed class DetectedObject
{
    /// <summary>Gets or sets the COCO class ID.</summary>
    public int ClassId { get; set; }

    /// <summary>Gets or sets the class name.</summary>
    public required string ClassName { get; set; }

    /// <summary>Gets or sets the detection confidence score.</summary>
    public float Confidence { get; set; }

    /// <summary>Gets or sets the left coordinate of the bounding box.</summary>
    public int X1 { get; set; }

    /// <summary>Gets or sets the top coordinate of the bounding box.</summary>
    public int Y1 { get; set; }

    /// <summary>Gets or sets the right coordinate of the bounding box.</summary>
    public int X2 { get; set; }

    /// <summary>Gets or sets the bottom coordinate of the bounding box.</summary>
    public int Y2 { get; set; }
}
