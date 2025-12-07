namespace VisualSearch.Api.Contracts.DTOs;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int CocoClassId,
    bool DetectionEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CategorySummaryDto(
    int Id,
    string Name,
    int CocoClassId,
    bool DetectionEnabled
);
