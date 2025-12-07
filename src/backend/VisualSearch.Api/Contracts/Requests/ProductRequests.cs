using System.ComponentModel.DataAnnotations;

namespace VisualSearch.Api.Contracts.Requests;

public record CreateProductRequest(
    [Required] string Name,
    string? Description,
    decimal? Price,
    string? Currency,
    string? ProductUrl,
    [Required] int ProviderId,
    int? CategoryId
);

public record UpdateProductRequest(
    [Required] string Name,
    string? Description,
    decimal? Price,
    string? Currency,
    string? ProductUrl,
    [Required] int ProviderId,
    int? CategoryId
);
