using System.ComponentModel.DataAnnotations;

namespace VisualSearch.Api.Contracts.Requests;

public record CreateCategoryRequest(
    [Required] string Name,
    string? Description,
    [Required] int CocoClassId,
    bool DetectionEnabled = true
);

public record UpdateCategoryRequest(
    [Required] string Name,
    string? Description,
    [Required] int CocoClassId,
    bool DetectionEnabled
);

public record ToggleDetectionRequest(
    bool DetectionEnabled
);
