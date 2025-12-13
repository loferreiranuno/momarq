namespace VisualSearch.Api.Contracts.DTOs;

/// <summary>
/// Admin-specific DTOs for dashboard and admin endpoints.
/// These maintain backwards compatibility with existing API responses.
/// </summary>
/// 
// ========== Dashboard Stats ==========

/// <summary>
/// Extended dashboard stats including vectorization progress.
/// </summary>
public record AdminDashboardStatsDto(
    int Products,
    int Providers,
    int Images,
    int VectorizedImages,
    double VectorizationProgress
);

// ========== Provider DTOs ==========

/// <summary>
/// Admin provider DTO with product count and crawler configuration.
/// </summary>
public record AdminProviderDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? LogoUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public int ProductCount { get; init; }
    public string? CrawlerType { get; init; }
    public string? CrawlerConfigJson { get; init; }
}

// ========== Category DTOs ==========

/// <summary>
/// Admin category DTO with product count.
/// </summary>
public record AdminCategoryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int CocoClassId { get; init; }
    public bool DetectionEnabled { get; init; }
    public int ProductCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

// ========== Product DTOs ==========

/// <summary>
/// Admin product DTO with full details including images.
/// </summary>
public record AdminProductDto
{
    public int Id { get; init; }
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public string? ExternalId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string? Currency { get; init; }
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? ProductUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<AdminProductImageDto> Images { get; init; } = [];
}

/// <summary>
/// Admin product image DTO with extended properties.
/// </summary>
public record AdminProductImageDto
{
    public int Id { get; init; }
    public required string ImageUrl { get; init; }
    public string? LocalPath { get; init; }
    public bool IsLocalFile { get; init; }
    public bool IsPrimary { get; init; }
    public bool HasEmbedding { get; init; }
    public DateTime CreatedAt { get; init; }
}

// ========== Paged Result ==========

/// <summary>
/// Admin paged result with list instead of IEnumerable.
/// </summary>
public record AdminPagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

// ========== Vectorization Results ==========

/// <summary>
/// Result of vectorizing a single product's images.
/// </summary>
public record ProductVectorizationResultDto(
    int ProductId,
    int TotalImages,
    int VectorizedImages
);

/// <summary>
/// Result of vectorizing all images.
/// </summary>
public record AllVectorizationResultDto(
    int TotalImages,
    int VectorizedImages,
    int SkippedOrFailed
);

// ========== System Status ==========

/// <summary>
/// Admin system status DTO.
/// </summary>
public record AdminSystemStatusDto(
    bool ClipModelLoaded,
    bool YoloModelLoaded,
    bool VectorizationAvailable,
    bool ObjectDetectionAvailable
);
