using System.ComponentModel.DataAnnotations;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Contracts.Requests;

/// <summary>
/// Valid crawler types for provider configuration.
/// </summary>
public static class AllowedCrawlerTypes
{
    public static readonly string[] Values = [
        CrawlerTypes.Generic,
        CrawlerTypes.JsonLd,
        CrawlerTypes.Sitemap,
        CrawlerTypes.Api,
        CrawlerTypes.ZaraHome
    ];

    public static bool IsValid(string? crawlerType) =>
        string.IsNullOrEmpty(crawlerType) || Values.Contains(crawlerType, StringComparer.OrdinalIgnoreCase);
}

public record CreateProviderRequest(
    [Required][StringLength(100, MinimumLength = 1)] string Name,
    [Url] string? WebsiteUrl,
    [Url] string? LogoUrl,
    string? CrawlerType,
    [MaxLength(10000)] string? CrawlerConfigJson
);

public record UpdateProviderRequest(
    [Required][StringLength(100, MinimumLength = 1)] string Name,
    [Url] string? WebsiteUrl,
    [Url] string? LogoUrl,
    string? CrawlerType,
    [MaxLength(10000)] string? CrawlerConfigJson
);
