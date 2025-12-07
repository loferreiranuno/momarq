namespace VisualSearch.Api.Contracts.DTOs;

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal? Price,
    string? Currency,
    string? ProductUrl,
    int ProviderId,
    string ProviderName,
    int? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IEnumerable<ProductImageDto> Images
);

public record ProductSummaryDto(
    int Id,
    string Name,
    decimal? Price,
    string? Currency,
    string ProviderName,
    string? CategoryName,
    string? PrimaryImageUrl
);
