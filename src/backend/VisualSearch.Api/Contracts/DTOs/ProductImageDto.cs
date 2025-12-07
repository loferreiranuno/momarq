namespace VisualSearch.Api.Contracts.DTOs;

public record ProductImageDto(
    int Id,
    string ImageUrl,
    string? AltText,
    bool IsPrimary,
    DateTime CreatedAt
);
