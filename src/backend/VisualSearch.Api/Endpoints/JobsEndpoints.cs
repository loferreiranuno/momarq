using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Services;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Job management endpoints for admin panel.
/// Provides CRUD and control operations for crawl jobs.
/// </summary>
public static class JobsEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the job endpoints to the application.
    /// </summary>
    public static void MapJobsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs")
            .WithTags("Jobs")
            .RequireAuthorization("Admin");

        // SSE endpoint (EventSource cannot send Authorization headers)
        app.MapGet("/api/jobs/sse", HandleJobsSseConnectionAsync)
            .AllowAnonymous()
            .Produces(200, contentType: "text/event-stream")
            .WithName("JobsSSE")
            .WithTags("Jobs")
            .WithDescription("Server-Sent Events endpoint for real-time jobs updates. Requires a short-lived SSE ticket.");

        // List jobs with pagination and filters
        group.MapGet("/", GetJobsAsync)
            .Produces<JobsListResponse>(200)
            .WithName("GetJobs")
            .WithDescription("Gets a paginated list of crawl jobs.");

        // Get job details
        group.MapGet("/{id:long}", GetJobByIdAsync)
            .Produces<CrawlJobDetailsDto>(200)
            .Produces(404)
            .WithName("GetJobById")
            .WithDescription("Gets detailed information about a specific job.");

        // Create a new crawl job
        group.MapPost("/", CreateJobAsync)
            .Produces<CrawlJobDto>(201)
            .Produces(400)
            .WithName("CreateJob")
            .WithDescription("Creates a new crawl job for a provider.");

        // Cancel a job
        group.MapPost("/{id:long}/cancel", CancelJobAsync)
            .Produces<CrawlJobDto>(200)
            .Produces(400)
            .Produces(404)
            .WithName("CancelJob")
            .WithDescription("Cancels a queued or running job.");

        // Pause a job
        group.MapPost("/{id:long}/pause", PauseJobAsync)
            .Produces<CrawlJobDto>(200)
            .Produces(400)
            .Produces(404)
            .WithName("PauseJob")
            .WithDescription("Pauses a queued or running job. Can be resumed later.");

        // Resume a paused job
        group.MapPost("/{id:long}/resume", ResumeJobAsync)
            .Produces<CrawlJobDto>(200)
            .Produces(400)
            .Produces(404)
            .WithName("ResumeJob")
            .WithDescription("Resumes a paused job.");

        // Retry a failed/canceled job
        group.MapPost("/{id:long}/retry", RetryJobAsync)
            .Produces<CrawlJobDto>(201)
            .Produces(400)
            .Produces(404)
            .WithName("RetryJob")
            .WithDescription("Creates a new job based on a failed or canceled job.");

        // Delete a job (only if completed/failed/canceled)
        group.MapDelete("/{id:long}", DeleteJobAsync)
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .WithName("DeleteJob")
            .WithDescription("Deletes a completed, failed, or canceled job.");

        // Get job statistics
        group.MapGet("/stats", GetJobStatsAsync)
            .Produces<JobStatsResponse>(200)
            .WithName("GetJobStats")
            .WithDescription("Gets job statistics summary.");
    }

    private static async Task HandleJobsSseConnectionAsync(
        HttpContext context,
        SseTicketService sseTicketService,
        VisualSearchDbContext db,
        [FromQuery] string ticket,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] CrawlJobStatus? status = null,
        [FromQuery] int? providerId = null,
        CancellationToken ct = default)
    {
        if (!sseTicketService.TryConsume("jobs", ticket, out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired SSE ticket" }, ct);
            return;
        }

        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await context.Response.Body.FlushAsync(ct);

        await using var writer = new StreamWriter(context.Response.Body);

        try
        {
            await WriteJobsSnapshotAsync(writer, db, page, pageSize, status, providerId, ct);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await WriteJobsSnapshotAsync(writer, db, page, pageSize, status, providerId, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        catch (Exception)
        {
            // Connection error
        }
    }

    private static async Task WriteJobsSnapshotAsync(
        StreamWriter writer,
        VisualSearchDbContext db,
        int page,
        int pageSize,
        CrawlJobStatus? status,
        int? providerId,
        CancellationToken ct)
    {
        var jobsResponse = await FetchJobsAsync(db, page, pageSize, status, providerId, ct);
        var statsResponse = await FetchJobStatsAsync(db, ct);

        var payload = new JobsSsePayload
        {
            Jobs = jobsResponse,
            Stats = statsResponse,
            TimestampUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, SseJsonOptions);

        await writer.WriteAsync("event: jobs-snapshot\n");
        await writer.WriteAsync($"data: {json}\n\n");
        await writer.FlushAsync();
    }

    private static async Task<JobsListResponse> FetchJobsAsync(
        VisualSearchDbContext db,
        int page,
        int pageSize,
        CrawlJobStatus? status,
        int? providerId,
        CancellationToken ct)
    {
        var query = db.CrawlJobs
            .Include(j => j.Provider)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (providerId.HasValue)
        {
            query = query.Where(j => j.ProviderId == providerId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new CrawlJobDto
            {
                Id = j.Id,
                ProviderId = j.ProviderId,
                Status = j.Status,
                CreatedAtUtc = j.CreatedAt,
                StartedAtUtc = j.StartedAt,
                FinishedAtUtc = j.CompletedAt ?? j.CanceledAt,
                PagesTotal = j.Pages.Count,
                PagesProcessed = j.Pages.Count(p => p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed),
                ProductsExtracted = j.ExtractedProducts.Count,
                ErrorsCount = j.Pages.Count(p => p.Status == CrawlPageStatus.Failed),
                LastError = j.ErrorMessage
            })
            .ToListAsync(ct);

        var providerIds = jobs.Select(j => j.ProviderId).Distinct().ToList();
        var providerNames = await db.Providers
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var jobsWithProvider = jobs.Select(j => new CrawlJobListItemDto
        {
            Id = j.Id,
            ProviderId = j.ProviderId,
            ProviderName = providerNames.GetValueOrDefault(j.ProviderId, "Unknown"),
            Status = j.Status,
            CreatedAtUtc = j.CreatedAtUtc,
            StartedAtUtc = j.StartedAtUtc,
            FinishedAtUtc = j.FinishedAtUtc,
            PagesTotal = j.PagesTotal,
            PagesProcessed = j.PagesProcessed,
            ProductsExtracted = j.ProductsExtracted,
            ErrorsCount = j.ErrorsCount,
            LastError = j.LastError
        }).ToList();

        return new JobsListResponse
        {
            Jobs = jobsWithProvider,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static async Task<JobStatsResponse> FetchJobStatsAsync(
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var stats = await db.CrawlJobs
            .GroupBy(_ => 1)
            .Select(g => new JobStatsResponse
            {
                TotalJobs = g.Count(),
                QueuedJobs = g.Count(j => j.Status == CrawlJobStatus.Queued),
                RunningJobs = g.Count(j => j.Status == CrawlJobStatus.Running),
                SucceededJobs = g.Count(j => j.Status == CrawlJobStatus.Succeeded),
                FailedJobs = g.Count(j => j.Status == CrawlJobStatus.Failed),
                CanceledJobs = g.Count(j => j.Status == CrawlJobStatus.Canceled)
            })
            .FirstOrDefaultAsync(ct);

        return stats ?? new JobStatsResponse();
    }

    private static async Task<IResult> GetJobsAsync(
        VisualSearchDbContext db,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] CrawlJobStatus? status = null,
        [FromQuery] int? providerId = null,
        CancellationToken ct = default)
    {
        var query = db.CrawlJobs
            .Include(j => j.Provider)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (providerId.HasValue)
        {
            query = query.Where(j => j.ProviderId == providerId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new CrawlJobDto
            {
                Id = j.Id,
                ProviderId = j.ProviderId,
                Status = j.Status,
                CreatedAtUtc = j.CreatedAt,
                StartedAtUtc = j.StartedAt,
                FinishedAtUtc = j.CompletedAt ?? j.CanceledAt,
                PagesTotal = j.Pages.Count,
                PagesProcessed = j.Pages.Count(p => p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed),
                ProductsExtracted = j.ExtractedProducts.Count,
                ErrorsCount = j.Pages.Count(p => p.Status == CrawlPageStatus.Failed),
                LastError = j.ErrorMessage
            })
            .ToListAsync(ct);

        // Attach provider names
        var providerIds = jobs.Select(j => j.ProviderId).Distinct().ToList();
        var providerNames = await db.Providers
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var jobsWithProvider = jobs.Select(j => new CrawlJobListItemDto
        {
            Id = j.Id,
            ProviderId = j.ProviderId,
            ProviderName = providerNames.GetValueOrDefault(j.ProviderId, "Unknown"),
            Status = j.Status,
            CreatedAtUtc = j.CreatedAtUtc,
            StartedAtUtc = j.StartedAtUtc,
            FinishedAtUtc = j.FinishedAtUtc,
            PagesTotal = j.PagesTotal,
            PagesProcessed = j.PagesProcessed,
            ProductsExtracted = j.ProductsExtracted,
            ErrorsCount = j.ErrorsCount,
            LastError = j.LastError
        }).ToList();

        return Results.Ok(new JobsListResponse
        {
            Jobs = jobsWithProvider,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> GetJobByIdAsync(
        long id,
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var job = await db.CrawlJobs
            .Include(j => j.Provider)
            .Include(j => j.Pages.OrderByDescending(p => p.CreatedAt).Take(100))
            .Include(j => j.ExtractedProducts)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        var dto = new CrawlJobDetailsDto
        {
            Job = new CrawlJobDto
            {
                Id = job.Id,
                ProviderId = job.ProviderId,
                Status = job.Status,
                CreatedAtUtc = job.CreatedAt,
                StartedAtUtc = job.StartedAt,
                FinishedAtUtc = job.CompletedAt ?? job.CanceledAt,
                PagesTotal = job.Pages.Count,
                PagesProcessed = job.Pages.Count(p => p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed),
                ProductsExtracted = job.ExtractedProducts.Count,
                ErrorsCount = job.Pages.Count(p => p.Status == CrawlPageStatus.Failed),
                LastError = job.ErrorMessage
            },
            Pages = job.Pages.Select(p => new CrawlPageDto
            {
                Id = p.Id,
                JobId = p.CrawlJobId,
                Url = p.Url,
                Status = p.Status,
                HttpStatusCode = p.HttpStatusCode,
                Error = p.ErrorMessage,
                StartedAtUtc = p.CreatedAt,
                FinishedAtUtc = p.FetchedAt,
                DurationMs = null
            }).ToList()
        };

        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateJobAsync(
        CreateCrawlJobRequest request,
        VisualSearchDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Validate provider exists
        var provider = await db.Providers.FindAsync([request.ProviderId], ct);
        if (provider is null)
        {
            return Results.BadRequest(new { error = "Provider not found" });
        }

        // Use provider website URL if startUrl not provided
        var startUrl = request.StartUrl ?? provider.WebsiteUrl;
        if (string.IsNullOrWhiteSpace(startUrl))
        {
            return Results.BadRequest(new { error = "StartUrl is required when provider has no website URL" });
        }

        // Get admin user ID from claims if available
        int? adminUserId = null;
        var userIdClaim = httpContext.User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            adminUserId = uid;
        }

        var job = new CrawlJob
        {
            ProviderId = request.ProviderId,
            StartUrl = startUrl,
            SitemapUrl = request.SitemapUrl,
            MaxPages = request.MaxPages,
            RequestedByAdminUserId = adminUserId,
            Status = CrawlJobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        db.CrawlJobs.Add(job);
        await db.SaveChangesAsync(ct);

        var dto = new CrawlJobDto
        {
            Id = job.Id,
            ProviderId = job.ProviderId,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAt,
            StartedAtUtc = null,
            FinishedAtUtc = null,
            PagesTotal = 0,
            PagesProcessed = 0,
            ProductsExtracted = 0,
            ErrorsCount = 0,
            LastError = null
        };

        return Results.Created($"/api/jobs/{job.Id}", dto);
    }

    private static async Task<IResult> CancelJobAsync(
        long id,
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var job = await db.CrawlJobs.FindAsync([id], ct);
        if (job is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (job.Status is not (CrawlJobStatus.Queued or CrawlJobStatus.Running))
        {
            return Results.BadRequest(new { error = "Can only cancel queued or running jobs" });
        }

        job.Status = CrawlJobStatus.Canceled;
        job.CanceledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var pagesCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id, ct);
        var pagesProcessed = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && (p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed), ct);
        var productsCount = await db.CrawlExtractedProducts.CountAsync(p => p.CrawlJobId == id, ct);
        var errorsCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && p.Status == CrawlPageStatus.Failed, ct);

        return Results.Ok(new CrawlJobDto
        {
            Id = job.Id,
            ProviderId = job.ProviderId,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAt,
            StartedAtUtc = job.StartedAt,
            FinishedAtUtc = job.CanceledAt,
            PagesTotal = pagesCount,
            PagesProcessed = pagesProcessed,
            ProductsExtracted = productsCount,
            ErrorsCount = errorsCount,
            LastError = job.ErrorMessage
        });
    }

    private static async Task<IResult> PauseJobAsync(
        long id,
        VisualSearchDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var job = await db.CrawlJobs.FindAsync([id], ct);
        if (job is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (job.Status is not (CrawlJobStatus.Queued or CrawlJobStatus.Running))
        {
            return Results.BadRequest(new { error = "Can only pause queued or running jobs" });
        }

        int? adminUserId = null;
        var userIdClaim = httpContext.User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            adminUserId = uid;
        }

        job.Status = CrawlJobStatus.Paused;
        job.PausedAt = DateTime.UtcNow;
        job.PausedByAdminUserId = adminUserId;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        await db.SaveChangesAsync(ct);

        var pagesCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id, ct);
        var pagesProcessed = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && (p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed), ct);
        var productsCount = await db.CrawlExtractedProducts.CountAsync(p => p.CrawlJobId == id, ct);
        var errorsCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && p.Status == CrawlPageStatus.Failed, ct);

        return Results.Ok(new CrawlJobDto
        {
            Id = job.Id,
            ProviderId = job.ProviderId,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAt,
            StartedAtUtc = job.StartedAt,
            FinishedAtUtc = job.PausedAt,
            PagesTotal = pagesCount,
            PagesProcessed = pagesProcessed,
            ProductsExtracted = productsCount,
            ErrorsCount = errorsCount,
            LastError = job.ErrorMessage
        });
    }

    private static async Task<IResult> ResumeJobAsync(
        long id,
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var job = await db.CrawlJobs.FindAsync([id], ct);
        if (job is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (job.Status != CrawlJobStatus.Paused)
        {
            return Results.BadRequest(new { error = "Can only resume paused jobs" });
        }

        job.Status = CrawlJobStatus.Queued;
        job.PausedAt = null;
        job.PausedByAdminUserId = null;
        await db.SaveChangesAsync(ct);

        var pagesCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id, ct);
        var pagesProcessed = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && (p.Status == CrawlPageStatus.Succeeded || p.Status == CrawlPageStatus.Failed), ct);
        var productsCount = await db.CrawlExtractedProducts.CountAsync(p => p.CrawlJobId == id, ct);
        var errorsCount = await db.CrawlPages.CountAsync(p => p.CrawlJobId == id && p.Status == CrawlPageStatus.Failed, ct);

        return Results.Ok(new CrawlJobDto
        {
            Id = job.Id,
            ProviderId = job.ProviderId,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAt,
            StartedAtUtc = job.StartedAt,
            FinishedAtUtc = null,
            PagesTotal = pagesCount,
            PagesProcessed = pagesProcessed,
            ProductsExtracted = productsCount,
            ErrorsCount = errorsCount,
            LastError = job.ErrorMessage
        });
    }

    private static async Task<IResult> RetryJobAsync(
        long id,
        VisualSearchDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var originalJob = await db.CrawlJobs.FindAsync([id], ct);
        if (originalJob is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (originalJob.Status is not (CrawlJobStatus.Failed or CrawlJobStatus.Canceled))
        {
            return Results.BadRequest(new { error = "Can only retry failed or canceled jobs" });
        }

        int? adminUserId = null;
        var userIdClaim = httpContext.User.FindFirst("userId")?.Value;
        if (int.TryParse(userIdClaim, out var uid))
        {
            adminUserId = uid;
        }

        var newJob = new CrawlJob
        {
            ProviderId = originalJob.ProviderId,
            StartUrl = originalJob.StartUrl,
            SitemapUrl = originalJob.SitemapUrl,
            MaxPages = originalJob.MaxPages,
            RequestedByAdminUserId = adminUserId,
            Status = CrawlJobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        db.CrawlJobs.Add(newJob);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/jobs/{newJob.Id}", new CrawlJobDto
        {
            Id = newJob.Id,
            ProviderId = newJob.ProviderId,
            Status = newJob.Status,
            CreatedAtUtc = newJob.CreatedAt,
            StartedAtUtc = null,
            FinishedAtUtc = null,
            PagesTotal = 0,
            PagesProcessed = 0,
            ProductsExtracted = 0,
            ErrorsCount = 0,
            LastError = null
        });
    }

    private static async Task<IResult> DeleteJobAsync(
        long id,
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var job = await db.CrawlJobs
            .Include(j => j.Pages)
            .Include(j => j.ExtractedProducts)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job is null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (job.Status is CrawlJobStatus.Queued or CrawlJobStatus.Running)
        {
            return Results.BadRequest(new { error = "Cannot delete queued or running jobs. Cancel first." });
        }

        db.CrawlExtractedProducts.RemoveRange(job.ExtractedProducts);
        db.CrawlPages.RemoveRange(job.Pages);
        db.CrawlJobs.Remove(job);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> GetJobStatsAsync(
        VisualSearchDbContext db,
        CancellationToken ct)
    {
        var stats = await db.CrawlJobs
            .GroupBy(_ => 1)
            .Select(g => new JobStatsResponse
            {
                TotalJobs = g.Count(),
                QueuedJobs = g.Count(j => j.Status == CrawlJobStatus.Queued),
                RunningJobs = g.Count(j => j.Status == CrawlJobStatus.Running),
                SucceededJobs = g.Count(j => j.Status == CrawlJobStatus.Succeeded),
                FailedJobs = g.Count(j => j.Status == CrawlJobStatus.Failed),
                CanceledJobs = g.Count(j => j.Status == CrawlJobStatus.Canceled),
                PausedJobs = g.Count(j => j.Status == CrawlJobStatus.Paused)
            })
            .FirstOrDefaultAsync(ct);

        return Results.Ok(stats ?? new JobStatsResponse());
    }
}

/// <summary>
/// Response for jobs list endpoint.
/// </summary>
public sealed record JobsListResponse
{
    public required IReadOnlyList<CrawlJobListItemDto> Jobs { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

/// <summary>
/// Job list item with provider name.
/// </summary>
public sealed record CrawlJobListItemDto
{
    public required long Id { get; init; }
    public required int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required CrawlJobStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? FinishedAtUtc { get; init; }
    public required int PagesTotal { get; init; }
    public required int PagesProcessed { get; init; }
    public required int ProductsExtracted { get; init; }
    public required int ErrorsCount { get; init; }
    public string? LastError { get; init; }
}

/// <summary>
/// Job statistics response.
/// </summary>
public sealed record JobStatsResponse
{
    public int TotalJobs { get; init; }
    public int QueuedJobs { get; init; }
    public int RunningJobs { get; init; }
    public int SucceededJobs { get; init; }
    public int FailedJobs { get; init; }
    public int CanceledJobs { get; init; }
    public int PausedJobs { get; init; }
}

public sealed record JobsSsePayload
{
    public required JobsListResponse Jobs { get; init; }
    public required JobStatsResponse Stats { get; init; }
    public required DateTime TimestampUtc { get; init; }
}
