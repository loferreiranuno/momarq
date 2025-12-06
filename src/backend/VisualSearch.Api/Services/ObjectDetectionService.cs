using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace VisualSearch.Api.Services;

/// <summary>
/// Service for detecting objects in images using YOLOv8 ONNX model.
/// Used to identify furniture items in room photos for targeted visual search.
/// </summary>
public sealed class ObjectDetectionService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly ILogger<ObjectDetectionService> _logger;
    private readonly bool _isModelLoaded;

    // YOLOv8 input size
    private const int InputSize = 640;
    
    // Confidence threshold for detections
    private const float ConfidenceThreshold = 0.25f;
    
    // IoU threshold for NMS
    private const float IouThreshold = 0.45f;

    // COCO class indices for furniture and home items we care about
    private static readonly HashSet<int> FurnitureClassIds = new()
    {
        56,  // chair
        57,  // couch/sofa
        58,  // potted plant
        59,  // bed
        60,  // dining table
        61,  // toilet
        62,  // tv
        63,  // laptop
        64,  // mouse
        65,  // remote
        66,  // keyboard
        67,  // cell phone
        68,  // microwave
        69,  // oven
        70,  // toaster
        71,  // sink
        72,  // refrigerator
        73,  // book
        74,  // clock
        75,  // vase
        76,  // scissors
        77,  // teddy bear
        78,  // hair drier
        79,  // toothbrush
    };

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
    public ObjectDetectionService(ILogger<ObjectDetectionService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;

        var modelPath = Path.Combine(environment.ContentRootPath, "Models", "yolov8n.onnx");

        if (File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    InterOpNumThreads = 2,
                    IntraOpNumThreads = 2
                };

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
    /// Detects objects in an image and returns bounding boxes for furniture items.
    /// </summary>
    /// <param name="imageBytes">The raw image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of detected objects with their bounding boxes and class names.</returns>
    public async Task<List<DetectedObject>> DetectObjectsAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!_isModelLoaded || _session is null)
        {
            _logger.LogDebug("YOLO model not loaded, returning empty detections.");
            return [];
        }

        try
        {
            return await Task.Run(() =>
            {
                using var image = Image.Load<Rgb24>(imageBytes);
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                // Preprocess image for YOLO
                var tensor = PreprocessImage(image);

                // Run inference
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", tensor)
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>();

                // Parse detections
                var detections = ParseDetections(output, originalWidth, originalHeight);

                // Apply NMS
                var nmsDetections = ApplyNms(detections);

                // Filter to furniture classes only
                var furnitureDetections = nmsDetections
                    .Where(d => FurnitureClassIds.Contains(d.ClassId))
                    .ToList();

                _logger.LogInformation("Detected {Count} furniture objects in image", furnitureDetections.Count);
                return furnitureDetections;

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
        var crops = new List<byte[]>();

        using var image = Image.Load<Rgb24>(imageBytes);

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
                using var crop = image.Clone(ctx => ctx.Crop(new Rectangle(x1, y1, cropWidth, cropHeight)));

                using var ms = new MemoryStream();
                crop.SaveAsJpeg(ms);
                crops.Add(ms.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to crop detection: {ClassName}", detection.ClassName);
            }
        }

        return crops;
    }

    /// <summary>
    /// Preprocesses an image for YOLOv8 inference.
    /// </summary>
    private static DenseTensor<float> PreprocessImage(Image<Rgb24> image)
    {
        // Resize to 640x640 with letterboxing
        var scale = Math.Min((float)InputSize / image.Width, (float)InputSize / image.Height);
        var newWidth = (int)(image.Width * scale);
        var newHeight = (int)(image.Height * scale);

        image.Mutate(x => x.Resize(newWidth, newHeight));

        // Create tensor with padding (letterbox)
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);

        // Fill with gray (114/255)
        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < InputSize; y++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    tensor[0, c, y, x] = 114f / 255f;
                }
            }
        }

        // Calculate offset for centering
        var offsetX = (InputSize - newWidth) / 2;
        var offsetY = (InputSize - newHeight) / 2;

        // Copy image pixels
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y + offsetY, x + offsetX] = pixel.R / 255f;
                    tensor[0, 1, y + offsetY, x + offsetX] = pixel.G / 255f;
                    tensor[0, 2, y + offsetY, x + offsetX] = pixel.B / 255f;
                }
            }
        });

        return tensor;
    }

    /// <summary>
    /// Parses YOLO output tensor into detection objects.
    /// </summary>
    private List<DetectedObject> ParseDetections(Tensor<float> output, int originalWidth, int originalHeight)
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

            if (maxScore < ConfidenceThreshold)
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
    private List<DetectedObject> ApplyNms(List<DetectedObject> detections)
    {
        var result = new List<DetectedObject>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = sorted.Where(d => CalculateIou(best, d) < IouThreshold).ToList();
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
