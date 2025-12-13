using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Crawling;

/// <summary>
/// Factory for resolving crawler strategies based on provider configuration.
/// </summary>
public interface ICrawlerStrategyFactory
{
    /// <summary>
    /// Gets the appropriate crawler strategy for the given configuration.
    /// </summary>
    /// <param name="config">The crawler configuration.</param>
    /// <returns>The crawler strategy to use.</returns>
    ICrawlerStrategy GetStrategy(CrawlerConfig config);

    /// <summary>
    /// Gets the appropriate crawler strategy for the given crawler type.
    /// </summary>
    /// <param name="crawlerType">The crawler type identifier.</param>
    /// <returns>The crawler strategy to use.</returns>
    ICrawlerStrategy GetStrategy(string crawlerType);
}

/// <summary>
/// Default implementation of <see cref="ICrawlerStrategyFactory"/>.
/// </summary>
public sealed class CrawlerStrategyFactory : ICrawlerStrategyFactory
{
    private readonly IEnumerable<ICrawlerStrategy> _strategies;
    private readonly ILogger<CrawlerStrategyFactory> _logger;

    public CrawlerStrategyFactory(
        IEnumerable<ICrawlerStrategy> strategies,
        ILogger<CrawlerStrategyFactory> logger)
    {
        _strategies = strategies;
        _logger = logger;
    }

    public ICrawlerStrategy GetStrategy(CrawlerConfig config)
    {
        return GetStrategy(config.CrawlerType);
    }

    public ICrawlerStrategy GetStrategy(string crawlerType)
    {
        var strategy = _strategies.FirstOrDefault(s =>
            string.Equals(s.CrawlerType, crawlerType, StringComparison.OrdinalIgnoreCase));

        if (strategy is null)
        {
            _logger.LogWarning(
                "No crawler strategy found for type '{CrawlerType}', falling back to generic",
                crawlerType);

            strategy = _strategies.FirstOrDefault(s =>
                string.Equals(s.CrawlerType, CrawlerTypes.Generic, StringComparison.OrdinalIgnoreCase));

            if (strategy is null)
            {
                throw new InvalidOperationException(
                    $"No crawler strategy registered for type '{crawlerType}' and no generic fallback available");
            }
        }

        return strategy;
    }
}
