using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Data;

/// <summary>
/// Entity Framework Core database context for the Visual Search application.
/// Includes pgvector support for CLIP embeddings and similarity search.
/// </summary>
public class VisualSearchDbContext : DbContext
{
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

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

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

            entity.Property(e => e.Category)
                .HasColumnName("category")
                .HasMaxLength(255);

            entity.Property(e => e.ProductUrl)
                .HasColumnName("product_url")
                .HasMaxLength(1024);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.Products)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProviderId);
            entity.HasIndex(e => e.Category);
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

            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(512)");

            entity.Property(e => e.IsPrimary)
                .HasColumnName("is_primary")
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.IsPrimary);

            // Note: HNSW index will be added in migration via raw SQL
        });
    }
}
