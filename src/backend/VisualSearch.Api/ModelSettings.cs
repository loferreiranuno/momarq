namespace VisualSearch.Api;

/// <summary>
/// Configuration settings for ML models and search parameters.
/// </summary>
public sealed class ModelSettings
{
    /// <summary>
    /// Gets or sets the CLIP model settings.
    /// </summary>
    public ClipSettings Clip { get; set; } = new();

    /// <summary>
    /// Gets or sets the YOLO model settings.
    /// </summary>
    public YoloSettings Yolo { get; set; } = new();

    /// <summary>
    /// Gets or sets the search settings.
    /// </summary>
    public SearchSettings Search { get; set; } = new();
}

/// <summary>
/// CLIP model ONNX runtime settings.
/// </summary>
public sealed class ClipSettings
{
    /// <summary>
    /// Gets or sets the number of threads for inter-op parallelism.
    /// 0 means use all available processors.
    /// </summary>
    public int InterOpNumThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the number of threads for intra-op parallelism.
    /// 0 means use all available processors.
    /// </summary>
    public int IntraOpNumThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether to enable memory pattern optimization.
    /// </summary>
    public bool EnableMemoryPattern { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable CPU memory arena.
    /// </summary>
    public bool EnableCpuMemArena { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum batch size for batch inference.
    /// </summary>
    public int MaxBatchSize { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether to use GPU acceleration (CUDA/DirectML).
    /// Requires Microsoft.ML.OnnxRuntime.Gpu or DirectML package.
    /// Phase 2 feature - currently not implemented.
    /// </summary>
    public bool UseGpu { get; set; } = false;

    /// <summary>
    /// Gets or sets the GPU device ID for CUDA/DirectML execution.
    /// Only used when UseGpu is true.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;
}

/// <summary>
/// YOLO model ONNX runtime settings.
/// </summary>
public sealed class YoloSettings
{
    /// <summary>
    /// Gets or sets the number of threads for inter-op parallelism.
    /// 0 means use all available processors.
    /// </summary>
    public int InterOpNumThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the number of threads for intra-op parallelism.
    /// 0 means use all available processors.
    /// </summary>
    public int IntraOpNumThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether to enable memory pattern optimization.
    /// </summary>
    public bool EnableMemoryPattern { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable CPU memory arena.
    /// </summary>
    public bool EnableCpuMemArena { get; set; } = true;

    /// <summary>
    /// Gets or sets the confidence threshold for detections.
    /// Higher values = fewer detections but higher quality.
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.40f;

    /// <summary>
    /// Gets or sets the IoU threshold for non-maximum suppression.
    /// </summary>
    public float IouThreshold { get; set; } = 0.45f;

    /// <summary>
    /// Gets or sets whether to use GPU acceleration (CUDA/DirectML).
    /// Requires Microsoft.ML.OnnxRuntime.Gpu or DirectML package.
    /// Phase 2 feature - currently not implemented.
    /// </summary>
    public bool UseGpu { get; set; } = false;

    /// <summary>
    /// Gets or sets the GPU device ID for CUDA/DirectML execution.
    /// Only used when UseGpu is true.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;
}

/// <summary>
/// Search settings for visual similarity queries.
/// </summary>
public sealed class SearchSettings
{
    /// <summary>
    /// Gets or sets the maximum results per detected object.
    /// </summary>
    public int MaxResultsPerObject { get; set; } = 8;

    /// <summary>
    /// Gets or sets the maximum total results across all detected objects.
    /// </summary>
    public int MaxTotalResults { get; set; } = 20;

    /// <summary>
    /// Gets or sets the minimum similarity threshold (0-1).
    /// </summary>
    public float MinSimilarityThreshold { get; set; } = 0.80f;

    /// <summary>
    /// Gets or sets the HNSW ef_search parameter for query-time recall/speed tradeoff.
    /// Higher values = better recall but slower. Default is 100.
    /// </summary>
    public int HnswEfSearch { get; set; } = 100;
}
