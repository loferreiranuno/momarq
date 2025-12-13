namespace VisualSearch.Contracts.Crawling;

/// <summary>
/// Represents the lifecycle state of a crawl job.
/// </summary>
public enum CrawlJobStatus
{
    /// <summary>The job was created and is waiting to be processed.</summary>
    Queued = 0,

    /// <summary>The worker has claimed the job and is processing it.</summary>
    Running = 1,

    /// <summary>The job completed successfully.</summary>
    Succeeded = 2,

    /// <summary>The job completed with a failure.</summary>
    Failed = 3,

    /// <summary>The job was canceled by an admin.</summary>
    Canceled = 4
}
