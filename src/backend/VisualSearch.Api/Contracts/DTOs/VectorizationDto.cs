namespace VisualSearch.Api.Contracts.DTOs;

/// <summary>
/// Result of a vectorization operation.
/// </summary>
public record VectorizationResultDto(
    int TotalProcessed,
    int Successful,
    int Failed,
    TimeSpan Duration
);

/// <summary>
/// Progress update during vectorization.
/// </summary>
public record VectorizationProgressDto(
    int Current,
    int Total,
    string? CurrentImageUrl,
    string Status
);
