using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Data;

/// <summary>
/// Entity Framework Core database context for the Visual Search application.
/// Includes pgvector support for CLIP embeddings and similarity search.
/// </summary>
public class VisualSearchDbContext : DbContext
{
    private const string CreatedAtColumnName = "created_at";
    private const string CurrentTimestampSql = "CURRENT_TIMESTAMP";

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualSearchDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public VisualSearchDbContext(DbContextOptions<VisualSearchDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the providers DbSet.
    /// </summary>
    public DbSet<Provider> Providers => Set<Provider>();

    /// <summary>
    /// Gets or sets the products DbSet.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Gets or sets the product images DbSet.
    /// </summary>
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    /// <summary>
    /// Gets or sets the settings DbSet.
    /// </summary>
    public DbSet<Setting> Settings => Set<Setting>();

    /// <summary>
    /// Gets or sets the admin users DbSet.
    /// </summary>
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    /// <summary>
    /// Gets or sets the categories DbSet.
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Gets or sets the crawl jobs DbSet.
    /// </summary>
    public DbSet<CrawlJob> CrawlJobs => Set<CrawlJob>();

    /// <summary>
    /// Gets or sets the crawl pages DbSet.
    /// </summary>
    public DbSet<CrawlPage> CrawlPages => Set<CrawlPage>();

    /// <summary>
    /// Gets or sets the extracted products from crawling DbSet.
    /// </summary>
    public DbSet<CrawlExtractedProduct> CrawlExtractedProducts => Set<CrawlExtractedProduct>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Configure Provider entity
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("providers");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.LogoUrl)
                .HasColumnName("logo_url")
                .HasMaxLength(1024);

            entity.Property(e => e.WebsiteUrl)
                .HasColumnName("website_url")
                .HasMaxLength(1024);

            entity.Property(e => e.CrawlerType)
                .HasColumnName("crawler_type")
                .HasMaxLength(50);

            entity.Property(e => e.CrawlerConfigJson)
                .HasColumnName("crawler_config_json")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasIndex(e => e.Name)
                .IsUnique();
        });

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.ProviderId)
                .HasColumnName("provider_id");

            entity.Property(e => e.ExternalId)
                .HasColumnName("external_id")
                .HasMaxLength(255);

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(4000);

            entity.Property(e => e.Price)
                .HasColumnName("price")
                .HasPrecision(10, 2);

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("EUR");

            entity.Property(e => e.CategoryId)
                .HasColumnName("category_id");

            entity.Property(e => e.ProductUrl)
                .HasColumnName("product_url")
                .HasMaxLength(1024);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.Products)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => new { e.ProviderId, e.ExternalId })
                .IsUnique()
                .HasFilter("external_id IS NOT NULL");
        });

        // Configure ProductImage entity
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("product_images");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.ProductId)
                .HasColumnName("product_id");

            entity.Property(e => e.ImageUrl)
                .HasColumnName("image_url")
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(e => e.LocalPath)
                .HasColumnName("local_path")
                .HasMaxLength(500);

            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(768)");

            entity.Property(e => e.IsPrimary)
                .HasColumnName("is_primary")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsPrimary);

            // Note: HNSW index will be added in migration via raw SQL
        });

        // Configure Setting entity
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("settings");

            entity.HasKey(e => e.Key);

            entity.Property(e => e.Key)
                .HasColumnName("key")
                .HasMaxLength(100);

            entity.Property(e => e.Value)
                .HasColumnName("value")
                .HasMaxLength(4000)
                .IsRequired();

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasDefaultValue(SettingType.String);

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(500);

            entity.Property(e => e.Category)
                .HasColumnName("category")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasIndex(e => e.Category);
        });

        // Configure AdminUser entity
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Username)
                .HasColumnName("username")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.MustChangePassword)
                .HasColumnName("must_change_password")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.Property(e => e.LastLoginAt)
                .HasColumnName("last_login_at");

            entity.HasIndex(e => e.Username)
                .IsUnique();
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.CocoClassId)
                .HasColumnName("coco_class_id")
                .IsRequired();

            entity.Property(e => e.DetectionEnabled)
                .HasColumnName("detection_enabled")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasIndex(e => e.CocoClassId)
                .IsUnique();

            entity.HasIndex(e => e.Name)
                .IsUnique();

            entity.HasIndex(e => e.DetectionEnabled);
        });

        // Configure CrawlJob entity
        modelBuilder.Entity<CrawlJob>(entity =>
        {
            entity.ToTable("crawl_jobs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.ProviderId)
                .HasColumnName("provider_id");

            entity.Property(e => e.RequestedByAdminUserId)
                .HasColumnName("requested_by_admin_user_id");

            entity.Property(e => e.StartUrl)
                .HasColumnName("start_url")
                .HasMaxLength(2048)
                .IsRequired();

            entity.Property(e => e.SitemapUrl)
                .HasColumnName("sitemap_url")
                .HasMaxLength(2048);

            entity.Property(e => e.MaxPages)
                .HasColumnName("max_pages");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<int>()
                .HasDefaultValue(CrawlJobStatus.Queued);

            entity.Property(e => e.LeaseOwner)
                .HasColumnName("lease_owner")
                .HasMaxLength(200);

            entity.Property(e => e.LeaseExpiresAt)
                .HasColumnName("lease_expires_at");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.CanceledAt)
                .HasColumnName("canceled_at");

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RequestedByAdminUser)
                .WithMany()
                .HasForeignKey(e => e.RequestedByAdminUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.LeaseExpiresAt });
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure CrawlPage entity
        modelBuilder.Entity<CrawlPage>(entity =>
        {
            entity.ToTable("crawl_pages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.CrawlJobId)
                .HasColumnName("crawl_job_id");

            entity.Property(e => e.Url)
                .HasColumnName("url")
                .HasMaxLength(2048)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<int>()
                .HasDefaultValue(CrawlPageStatus.Queued);

            entity.Property(e => e.HttpStatusCode)
                .HasColumnName("http_status_code");

            entity.Property(e => e.ContentType)
                .HasColumnName("content_type")
                .HasMaxLength(255);

            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(512);

            entity.Property(e => e.ContentSha256)
                .HasColumnName("content_sha256")
                .HasMaxLength(64);

            entity.Property(e => e.Content)
                .HasColumnName("content")
                .HasColumnType("text");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.Property(e => e.FetchedAt)
                .HasColumnName("fetched_at");

            entity.HasOne(e => e.CrawlJob)
                .WithMany(j => j.Pages)
                .HasForeignKey(e => e.CrawlJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CrawlJobId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.CrawlJobId, e.Url }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure CrawlExtractedProduct entity
        modelBuilder.Entity<CrawlExtractedProduct>(entity =>
        {
            entity.ToTable("crawl_extracted_products");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.CrawlJobId)
                .HasColumnName("crawl_job_id");

            entity.Property(e => e.CrawlPageId)
                .HasColumnName("crawl_page_id");

            entity.Property(e => e.ProviderId)
                .HasColumnName("provider_id");

            entity.Property(e => e.ExternalId)
                .HasColumnName("external_id")
                .HasMaxLength(255);

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(500);

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(4000);

            entity.Property(e => e.Price)
                .HasColumnName("price")
                .HasPrecision(10, 2);

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3);

            entity.Property(e => e.ProductUrl)
                .HasColumnName("product_url")
                .HasMaxLength(2048);

            entity.Property(e => e.ImageUrlsJson)
                .HasColumnName("image_urls_json")
                .HasColumnType("text");

            entity.Property(e => e.RawJson)
                .HasColumnName("raw_json")
                .HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasColumnName(CreatedAtColumnName)
                .HasDefaultValueSql(CurrentTimestampSql);

            entity.HasOne(e => e.CrawlJob)
                .WithMany(j => j.ExtractedProducts)
                .HasForeignKey(e => e.CrawlJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CrawlPage)
                .WithMany(p => p.ExtractedProducts)
                .HasForeignKey(e => e.CrawlPageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CrawlJobId);
            entity.HasIndex(e => e.CrawlPageId);
            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => new { e.ProviderId, e.ExternalId })
                .HasFilter("external_id IS NOT NULL");
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
