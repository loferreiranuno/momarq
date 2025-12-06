using Microsoft.Extensions.Configuration;

namespace VisualSearch.Api;

/// <summary>
/// JWT configuration settings with sensible defaults for local development.
/// </summary>
public sealed record JwtOptions
{
    private const string DefaultKey = "VisualSearch-Default-JWT-Key-Change-In-Production-2024!";
    private const string DefaultIssuer = "VisualSearch.Api";
    private const string DefaultAudience = "VisualSearch.Frontend";

    public string Key { get; init; } = string.Empty;

    public string Issuer { get; init; } = DefaultIssuer;

    public string Audience { get; init; } = DefaultAudience;

    /// <summary>
    /// Creates options from configuration, applying defaults when values are missing.
    /// </summary>
    public static JwtOptions Create(IConfiguration configuration)
    {
        var options = new JwtOptions();
        configuration.GetSection("Jwt").Bind(options);

        var key = string.IsNullOrWhiteSpace(configuration["Jwt:Key"]) ? options.Key : configuration["Jwt:Key"] ?? string.Empty;
        var issuer = string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]) ? options.Issuer : configuration["Jwt:Issuer"] ?? DefaultIssuer;
        var audience = string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]) ? options.Audience : configuration["Jwt:Audience"] ?? DefaultAudience;

        key = string.IsNullOrWhiteSpace(key) ? DefaultKey : key;

        return options with
        {
            Key = key,
            Issuer = issuer,
            Audience = audience
        };
    }
}
