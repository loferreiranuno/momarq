namespace VisualSearch.Api.Contracts.DTOs;

public record ProviderDto(
    int Id,
    string Name,
    string? Description,
    string? WebsiteUrl,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int ProductCount
);

public record ProviderSummaryDto(
    int Id,
    string Name,
    bool IsActive
);
