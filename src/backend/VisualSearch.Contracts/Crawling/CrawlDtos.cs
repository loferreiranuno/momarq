namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Represents a crawl job summary.
/// </summary>
public sealed record CrawlJobDto
{
    /// <summary>Gets the crawl job identifier.</summary>
    public required long Id { get; init; }

    /// <summary>Gets the provider id associated with the job.</summary>
    public required int ProviderId { get; init; }

    /// <summary>Gets the job status.</summary>
    public required CrawlJobStatus Status { get; init; }

    /// <summary>Gets when the job was created (UTC).</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>Gets when the job started (UTC).</summary>
    public DateTime? StartedAtUtc { get; init; }

    /// <summary>Gets when the job finished (UTC).</summary>
    public DateTime? FinishedAtUtc { get; init; }

    /// <summary>Gets the number of pages queued/known for this job.</summary>
    public required int PagesTotal { get; init; }

    /// <summary>Gets the number of pages processed for this job.</summary>
    public required int PagesProcessed { get; init; }

    /// <summary>Gets the number of products extracted for this job.</summary>
    public required int ProductsExtracted { get; init; }

    /// <summary>Gets the number of errors recorded for this job.</summary>
    public required int ErrorsCount { get; init; }

    /// <summary>Gets the last error message if any.</summary>
    public string? LastError { get; init; }
}

/// <summary>
/// Represents a crawl job with associated page audit entries.
/// </summary>
public sealed record CrawlJobDetailsDto
{
    /// <summary>Gets the job summary.</summary>
    public required CrawlJobDto Job { get; init; }

    /// <summary>Gets the audited pages for this job.</summary>
    public required IReadOnlyList<CrawlPageDto> Pages { get; init; }
}

/// <summary>
/// Represents an audited page within a crawl job.
/// </summary>
public sealed record CrawlPageDto
{
    /// <summary>Gets the page identifier.</summary>
    public required long Id { get; init; }

    /// <summary>Gets the owning crawl job identifier.</summary>
    public required long JobId { get; init; }

    /// <summary>Gets the page URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the page status.</summary>
    public required CrawlPageStatus Status { get; init; }

    /// <summary>Gets the HTTP status code if available.</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>Gets the last error if any.</summary>
    public string? Error { get; init; }

    /// <summary>Gets when processing started (UTC).</summary>
    public DateTime? StartedAtUtc { get; init; }

    /// <summary>Gets when processing finished (UTC).</summary>
    public DateTime? FinishedAtUtc { get; init; }

    /// <summary>Gets the duration in milliseconds if available.</summary>
    public int? DurationMs { get; init; }
}
