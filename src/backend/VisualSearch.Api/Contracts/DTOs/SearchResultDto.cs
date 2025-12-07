namespace VisualSearch.Api.Contracts.DTOs;

public record SearchResultDto(
    int ProductId,
    string ProductName,
    decimal? Price,
    string? Currency,
    string ProviderName,
    string? CategoryName,
    string ImageUrl,
    double Similarity
);
