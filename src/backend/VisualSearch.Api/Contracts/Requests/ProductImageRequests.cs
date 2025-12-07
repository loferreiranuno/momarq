using System.ComponentModel.DataAnnotations;

namespace VisualSearch.Api.Contracts.Requests;

public record CreateProductImageRequest(
    [Required] int ProductId,
    [Required] string ImageUrl,
    string? AltText,
    bool IsPrimary = false
);

public record UpdateProductImageRequest(
    string? AltText,
    bool IsPrimary
);
