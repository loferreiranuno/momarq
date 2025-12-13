using Microsoft.EntityFrameworkCore;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Worker.Data;

// Using enums from VisualSearch.Contracts.Crawling:
// - CrawlJobStatus (Queued, Running, Succeeded, Failed, Canceled)
// - CrawlPageStatus (Queued, Processing, Succeeded, Skipped, Failed)

/// <summary>
/// Database context for the worker service.
/// Contains only entities needed for crawl job processing.
/// </summary>
public sealed class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
    {
    }

    public DbSet<CrawlJobEntity> CrawlJobs => Set<CrawlJobEntity>();
    public DbSet<CrawlPageEntity> CrawlPages => Set<CrawlPageEntity>();
    public DbSet<CrawlExtractedProductEntity> CrawlExtractedProducts => Set<CrawlExtractedProductEntity>();
    public DbSet<ProviderEntity> Providers => Set<ProviderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CrawlJob
        modelBuilder.Entity<CrawlJobEntity>(entity =>
        {
            entity.ToTable("crawl_jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.RequestedByAdminUserId).HasColumnName("requested_by_admin_user_id");
            entity.Property(e => e.StartUrl).HasColumnName("start_url").HasMaxLength(2048);
            entity.Property(e => e.SitemapUrl).HasColumnName("sitemap_url").HasMaxLength(2048);
            entity.Property(e => e.MaxPages).HasColumnName("max_pages");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);
            entity.Property(e => e.LeaseExpiresAt).HasColumnName("lease_expires_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CanceledAt).HasColumnName("canceled_at");

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.LeaseExpiresAt });
        });

        // CrawlPage
        modelBuilder.Entity<CrawlPageEntity>(entity =>
        {
            entity.ToTable("crawl_pages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CrawlJobId).HasColumnName("crawl_job_id");
            entity.Property(e => e.Url).HasColumnName("url").HasMaxLength(2048);
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.HttpStatusCode).HasColumnName("http_status_code");
            entity.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(255);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(512);
            entity.Property(e => e.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64);
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");

            entity.HasOne(e => e.CrawlJob)
                .WithMany(j => j.Pages)
                .HasForeignKey(e => e.CrawlJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CrawlJobId);
            entity.HasIndex(e => new { e.CrawlJobId, e.Url }).IsUnique();
        });

        // CrawlExtractedProduct
        modelBuilder.Entity<CrawlExtractedProductEntity>(entity =>
        {
            entity.ToTable("crawl_extracted_products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CrawlJobId).HasColumnName("crawl_job_id");
            entity.Property(e => e.CrawlPageId).HasColumnName("crawl_page_id");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.ExternalId).HasColumnName("external_id").HasMaxLength(255);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(4000);
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(e => e.ProductUrl).HasColumnName("product_url").HasMaxLength(2048);
            entity.Property(e => e.ImageUrlsJson).HasColumnName("image_urls_json");
            entity.Property(e => e.RawJson).HasColumnName("raw_json");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.CrawlJob)
                .WithMany(j => j.ExtractedProducts)
                .HasForeignKey(e => e.CrawlJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CrawlPage)
                .WithMany(p => p.ExtractedProducts)
                .HasForeignKey(e => e.CrawlPageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CrawlJobId);
            entity.HasIndex(e => e.CrawlPageId);
        });

        // Provider (read-only for worker)
        modelBuilder.Entity<ProviderEntity>(entity =>
        {
            entity.ToTable("providers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.WebsiteUrl).HasColumnName("website_url").HasMaxLength(1024);
            entity.Property(e => e.CrawlerType).HasColumnName("crawler_type").HasMaxLength(50);
            entity.Property(e => e.CrawlerConfigJson).HasColumnName("crawler_config_json");
        });
    }
}

/// <summary>
/// Crawl job entity for worker context.
/// </summary>
public sealed class CrawlJobEntity
{
    public long Id { get; set; }
    public int ProviderId { get; set; }
    public int? RequestedByAdminUserId { get; set; }
    public required string StartUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public int? MaxPages { get; set; }
    public CrawlJobStatus Status { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    public ProviderEntity? Provider { get; set; }
    public ICollection<CrawlPageEntity> Pages { get; set; } = [];
    public ICollection<CrawlExtractedProductEntity> ExtractedProducts { get; set; } = [];
}

/// <summary>
/// Crawl page entity for worker context.
/// </summary>
public sealed class CrawlPageEntity
{
    public long Id { get; set; }
    public long CrawlJobId { get; set; }
    public required string Url { get; set; }
    public CrawlPageStatus Status { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ContentType { get; set; }
    public string? Title { get; set; }
    public string? ContentSha256 { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FetchedAt { get; set; }

    public CrawlJobEntity? CrawlJob { get; set; }
    public ICollection<CrawlExtractedProductEntity> ExtractedProducts { get; set; } = [];
}

/// <summary>
/// Extracted product entity for worker context.
/// </summary>
public sealed class CrawlExtractedProductEntity
{
    public long Id { get; set; }
    public long CrawlJobId { get; set; }
    public long CrawlPageId { get; set; }
    public int ProviderId { get; set; }
    public string? ExternalId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? ProductUrl { get; set; }
    public string? ImageUrlsJson { get; set; }
    public string? RawJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public CrawlJobEntity? CrawlJob { get; set; }
    public CrawlPageEntity? CrawlPage { get; set; }
}

/// <summary>
/// Provider entity for worker context (read-only).
/// </summary>
public sealed class ProviderEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? CrawlerType { get; set; }
    public string? CrawlerConfigJson { get; set; }
}
