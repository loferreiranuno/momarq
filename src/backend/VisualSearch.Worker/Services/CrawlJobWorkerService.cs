using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VisualSearch.Contracts.Crawling;
using VisualSearch.Worker.Crawling;
using VisualSearch.Worker.Data;

namespace VisualSearch.Worker.Services;

/// <summary>
/// Background service that claims and processes crawl jobs.
/// Implements lease-based job claiming for distributed safety.
/// </summary>
public sealed class CrawlJobWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICrawlerStrategyFactory _crawlerStrategyFactory;
    private readonly ILogger<CrawlJobWorkerService> _logger;
    private readonly WorkerOptions _options;

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public CrawlJobWorkerService(
        IServiceScopeFactory scopeFactory,
        ICrawlerStrategyFactory crawlerStrategyFactory,
        ILogger<CrawlJobWorkerService> logger,
        IOptions<WorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _crawlerStrategyFactory = crawlerStrategyFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Crawl job worker starting. Worker ID: {WorkerId}", _options.WorkerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobClaimed = await TryClaimAndProcessJobAsync(stoppingToken);

                // If no job was available, wait before polling again
                if (!jobClaimed)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Crawl job worker stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in crawl job worker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Crawl job worker stopped");
    }

    private async Task<bool> TryClaimAndProcessJobAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();

        // Try to claim a job using optimistic concurrency
        var job = await TryClaimJobAsync(db, stoppingToken);
        if (job == null)
        {
            return false;
        }

        _logger.LogInformation(
            "Claimed job {JobId} for provider {ProviderId}. Start URL: {StartUrl}",
            job.Id, job.ProviderId, job.StartUrl);

        // Create a linked token that will be cancelled if the job is cancelled
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var jobToken = jobCts.Token;

        // Start lease renewal task
        var leaseRenewalTask = RenewLeaseAsync(job.Id, jobToken);

        try
        {
            await ProcessJobAsync(db, job, jobToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Job {JobId} interrupted due to worker shutdown", job.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed with error", job.Id);
            await MarkJobFailedAsync(db, job.Id, ex.Message, stoppingToken);
        }
        finally
        {
            // Stop lease renewal
            await jobCts.CancelAsync();
            try { await leaseRenewalTask; } catch { /* ignore */ }
        }

        return true;
    }

    private async Task<CrawlJobEntity?> TryClaimJobAsync(WorkerDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leaseExpiry = now.Add(LeaseDuration);

        // Find a queued job or one with expired lease
        var job = await db.CrawlJobs
            .Where(j =>
                j.Status == CrawlJobStatus.Queued ||
                (j.Status == CrawlJobStatus.Running && j.LeaseExpiresAt < now))
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (job == null) return null;

        // Try to claim it (optimistic concurrency via row version or just update)
        var previousStatus = job.Status;
        var previousLease = job.LeaseOwner;

        job.Status = CrawlJobStatus.Running;
        job.LeaseOwner = _options.WorkerId;
        job.LeaseExpiresAt = leaseExpiry;
        job.StartedAt ??= now;

        try
        {
            await db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Successfully claimed job {JobId}. Previous status: {PreviousStatus}, Previous owner: {PreviousOwner}",
                job.Id, previousStatus, previousLease);

            // Load provider for configuration
            await db.Entry(job).Reference(j => j.Provider).LoadAsync(ct);

            return job;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug("Failed to claim job {JobId} due to concurrency conflict", job.Id);
            return null;
        }
    }

    private async Task RenewLeaseAsync(long jobId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseRenewalInterval, ct);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();

                var job = await db.CrawlJobs.FindAsync([jobId], ct);
                if (job == null || job.LeaseOwner != _options.WorkerId)
                {
                    _logger.LogWarning("Lost lease on job {JobId}", jobId);
                    return;
                }

                // Check if job was cancelled
                if (job.Status == CrawlJobStatus.Canceled)
                {
                    _logger.LogInformation("Job {JobId} was cancelled externally", jobId);
                    return;
                }

                job.LeaseExpiresAt = DateTime.UtcNow.Add(LeaseDuration);
                await db.SaveChangesAsync(ct);

                _logger.LogDebug("Renewed lease on job {JobId}", jobId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error renewing lease on job {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(WorkerDbContext db, CrawlJobEntity job, CancellationToken ct)
    {
        // Get crawler configuration
        var config = GetCrawlerConfig(job.Provider);
        var strategy = _crawlerStrategyFactory.GetStrategy(config);

        _logger.LogInformation(
            "Processing job {JobId} with strategy '{CrawlerType}'",
            job.Id, strategy.CrawlerType);

        // Phase 1: Discover URLs
        var urls = await strategy.DiscoverUrlsAsync(job.StartUrl, job.SitemapUrl, config, ct);

        // Apply max pages limit
        if (job.MaxPages.HasValue && urls.Count > job.MaxPages.Value)
        {
            urls = urls.Take(job.MaxPages.Value).ToList();
        }

        _logger.LogInformation("Job {JobId}: {UrlCount} URLs to process", job.Id, urls.Count);

        // Create page records for tracking
        var existingUrls = await db.CrawlPages
            .Where(p => p.CrawlJobId == job.Id)
            .Select(p => p.Url)
            .ToHashSetAsync(ct);

        var newPages = urls
            .Where(u => !existingUrls.Contains(u))
            .Select(u => new CrawlPageEntity
            {
                CrawlJobId = job.Id,
                Url = u,
                Status = CrawlPageStatus.Queued,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (newPages.Count > 0)
        {
            db.CrawlPages.AddRange(newPages);
            await db.SaveChangesAsync(ct);
        }

        // Phase 2: Process pages
        var pagesToProcess = await db.CrawlPages
            .Where(p => p.CrawlJobId == job.Id && p.Status == CrawlPageStatus.Queued)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var processedCount = 0;
        var errorCount = 0;

        foreach (var page in pagesToProcess)
        {
            if (ct.IsCancellationRequested) break;

            // Check if job was cancelled or paused
            var currentJob = await db.CrawlJobs.FindAsync([job.Id], ct);
            if (currentJob?.Status == CrawlJobStatus.Canceled)
            {
                _logger.LogInformation("Job {JobId} was cancelled, stopping processing", job.Id);
                break;
            }
            if (currentJob?.Status == CrawlJobStatus.Paused)
            {
                _logger.LogInformation("Job {JobId} was paused, stopping processing. Job can be resumed later.", job.Id);
                break;
            }

            // Process page
            page.Status = CrawlPageStatus.Processing;
            await db.SaveChangesAsync(ct);

            try
            {
                var result = await strategy.CrawlPageAsync(page.Url, config, ct);

                page.HttpStatusCode = result.HttpStatusCode;
                page.ContentType = result.ContentType;
                page.Title = result.Title;
                page.ContentSha256 = result.ContentHash;
                page.FetchedAt = DateTime.UtcNow;

                if (result.Success)
                {
                    page.Status = CrawlPageStatus.Succeeded;
                    // Clear content after successful extraction (storage optimization)
                    page.Content = null;

                    // Save extracted products
                    foreach (var product in result.Products)
                    {
                        var extracted = new CrawlExtractedProductEntity
                        {
                            CrawlJobId = job.Id,
                            CrawlPageId = page.Id,
                            ProviderId = job.ProviderId,
                            ExternalId = product.ExternalId,
                            Name = product.Name,
                            Description = product.Description,
                            Price = product.Price,
                            Currency = product.Currency,
                            ProductUrl = product.ProductUrl,
                            ImageUrlsJson = product.ImageUrls.Count > 0
                                ? JsonSerializer.Serialize(product.ImageUrls)
                                : null,
                            RawJson = product.RawJson,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.CrawlExtractedProducts.Add(extracted);
                    }

                    // Queue newly discovered URLs (up to max pages)
                    if (result.DiscoveredUrls.Count > 0 && !job.MaxPages.HasValue)
                    {
                        var currentPageCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == job.Id, ct);
                        var existingUrlSet = await db.CrawlPages
                            .Where(p => p.CrawlJobId == job.Id)
                            .Select(p => p.Url)
                            .ToHashSetAsync(ct);

                        var newDiscoveredPages = result.DiscoveredUrls
                            .Where(u => !existingUrlSet.Contains(u))
                            .Take(Math.Max(0, (_options.MaxPagesPerJob ?? 1000) - currentPageCount))
                            .Select(u => new CrawlPageEntity
                            {
                                CrawlJobId = job.Id,
                                Url = u,
                                Status = CrawlPageStatus.Queued,
                                CreatedAt = DateTime.UtcNow
                            })
                            .ToList();

                        if (newDiscoveredPages.Count > 0)
                        {
                            db.CrawlPages.AddRange(newDiscoveredPages);
                        }
                    }

                    _logger.LogDebug(
                        "Page {PageId} ({Url}): {ProductCount} products extracted",
                        page.Id, page.Url, result.Products.Count);
                }
                else
                {
                    page.Status = CrawlPageStatus.Failed;
                    page.ErrorMessage = result.Error;
                    // Keep content for failed pages for debugging purposes
                    page.Content = result.Content;
                    errorCount++;

                    _logger.LogWarning(
                        "Page {PageId} ({Url}) failed: {Error}",
                        page.Id, page.Url, result.Error);
                }

                await db.SaveChangesAsync(ct);
                processedCount++;

                // Respect rate limiting
                if (config.RequestDelayMs > 0)
                {
                    await Task.Delay(config.RequestDelayMs, ct);
                }
            }
            catch (Exception ex)
            {
                page.Status = CrawlPageStatus.Failed;
                page.ErrorMessage = ex.Message;
                page.FetchedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                errorCount++;

                _logger.LogError(ex, "Error processing page {PageId} ({Url})", page.Id, page.Url);

                // Stop job immediately on critical errors (OutOfMemoryException, etc.)
                if (IsCriticalError(ex))
                {
                    _logger.LogCritical(ex, "Critical error detected, stopping job {JobId}", job.Id);
                    await MarkJobFailedAsync(db, job.Id, $"Critical error: {ex.GetType().Name} - {ex.Message}", ct);
                    return; // Exit the processing loop
                }
            }
        }

        // Mark job as completed
        var finalJob = await db.CrawlJobs.FindAsync([job.Id], ct);
        if (finalJob != null && finalJob.Status == CrawlJobStatus.Running)
        {
            var isFailed = errorCount > 0 && processedCount == errorCount;
            finalJob.Status = isFailed ? CrawlJobStatus.Failed : CrawlJobStatus.Succeeded;
            finalJob.CompletedAt = DateTime.UtcNow;
            finalJob.LeaseOwner = null;
            finalJob.LeaseExpiresAt = null;

            // Set error message if job failed
            if (isFailed)
            {
                finalJob.ErrorMessage = await BuildJobErrorSummaryAsync(db, job.Id, errorCount, processedCount, ct);
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Job {JobId} completed. Status: {Status}, Processed: {Processed}, Errors: {Errors}",
                job.Id, finalJob.Status, processedCount, errorCount);
        }
    }

    private async Task MarkJobFailedAsync(WorkerDbContext db, long jobId, string error, CancellationToken ct)
    {
        var job = await db.CrawlJobs.FindAsync([jobId], ct);
        if (job != null)
        {
            job.Status = CrawlJobStatus.Failed;
            job.ErrorMessage = error;
            job.CompletedAt = DateTime.UtcNow;
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;

            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<string> BuildJobErrorSummaryAsync(
        WorkerDbContext db,
        long jobId,
        int errorCount,
        int processedCount,
        CancellationToken ct)
    {
        if (processedCount == 0)
        {
            return "No pages could be crawled.";
        }

        // Get up to 5 unique error messages from failed pages
        var failedPageErrors = await db.CrawlPages
            .Where(p => p.CrawlJobId == jobId && p.Status == CrawlPageStatus.Failed && p.ErrorMessage != null)
            .Select(p => p.ErrorMessage!)
            .Distinct()
            .Take(5)
            .ToListAsync(ct);

        if (failedPageErrors.Count == 0)
        {
            return $"Failed {errorCount} of {processedCount} pages.";
        }

        var errorSummary = string.Join("; ", failedPageErrors.Select(e => 
            e.Length > 100 ? e.Substring(0, 97) + "..." : e));

        var summary = $"Failed {errorCount} of {processedCount} pages. Errors: {errorSummary}";

        // Truncate to database field limit (2000 chars)
        if (summary.Length > 2000)
        {
            summary = summary.Substring(0, 1997) + "...";
        }

        return summary;
    }

    private static CrawlerConfig GetCrawlerConfig(ProviderEntity? provider)
    {
        if (provider == null || string.IsNullOrWhiteSpace(provider.CrawlerConfigJson))
        {
            return new CrawlerConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<CrawlerConfig>(provider.CrawlerConfigJson)
                ?? new CrawlerConfig();
        }
        catch
        {
            return new CrawlerConfig();
        }
    }

    /// <summary>
    /// Determines if an exception is critical and should stop the job immediately.
    /// Critical errors indicate resource exhaustion or unrecoverable states.
    /// </summary>
    private static bool IsCriticalError(Exception ex)
    {
        return ex is OutOfMemoryException
            or StackOverflowException
            or InsufficientMemoryException
            or AccessViolationException
            // Also check inner exceptions for wrapped critical errors
            || (ex.InnerException is not null && IsCriticalError(ex.InnerException));
    }
}

/// <summary>
/// Worker configuration options.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>
    /// Unique identifier for this worker instance.
    /// </summary>
    public string WorkerId { get; set; } = $"worker-{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// Poll interval in seconds when no jobs are available.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Lease duration in minutes for job locking.
    /// </summary>
    public int LeaseMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum pages to crawl per job (safety limit).
    /// </summary>
    public int? MaxPagesPerJob { get; set; } = 1000;
}
