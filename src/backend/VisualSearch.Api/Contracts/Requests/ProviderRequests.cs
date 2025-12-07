using System.ComponentModel.DataAnnotations;

namespace VisualSearch.Api.Contracts.Requests;

public record CreateProviderRequest(
    [Required] string Name,
    string? Description,
    string? WebsiteUrl,
    string? LogoUrl,
    bool IsActive = true
);

public record UpdateProviderRequest(
    [Required] string Name,
    string? Description,
    string? WebsiteUrl,
    string? LogoUrl,
    bool IsActive
);
