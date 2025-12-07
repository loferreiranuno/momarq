namespace VisualSearch.Api.Contracts.DTOs;

public record DashboardStatsDto(
    int TotalProviders,
    int ActiveProviders,
    int TotalProducts,
    int TotalImages,
    int TotalCategories,
    int EnabledDetectionCategories
);
