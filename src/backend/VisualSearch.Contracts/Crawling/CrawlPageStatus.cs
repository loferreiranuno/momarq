namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Represents the processing state of an individual crawled page within a job.
/// </summary>
public enum CrawlPageStatus
{
    /// <summary>The page is queued for processing.</summary>
    Queued = 0,

    /// <summary>The page is being fetched or parsed.</summary>
    Processing = 1,

    /// <summary>The page was fetched and parsed successfully.</summary>
    Succeeded = 2,

    /// <summary>The page was skipped (e.g., out of scope or duplicate).</summary>
    Skipped = 3,

    /// <summary>The page processing failed.</summary>
    Failed = 4
}
