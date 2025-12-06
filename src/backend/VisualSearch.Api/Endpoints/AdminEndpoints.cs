using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Admin endpoints for managing providers, products, and product images.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>
    /// Maps the admin endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin");

        // Provider endpoints
        group.MapGet("/providers", GetProvidersAsync)
            .WithName("GetProviders")
            .WithDescription("Gets all providers.");

        group.MapPost("/providers", CreateProviderAsync)
            .WithName("CreateProvider")
            .WithDescription("Creates a new provider.");

        // Product endpoints
        group.MapGet("/products", GetProductsAsync)
            .WithName("GetProducts")
            .WithDescription("Gets products with pagination.");

        group.MapGet("/products/{id:int}", GetProductByIdAsync)
            .WithName("GetProductById")
            .WithDescription("Gets a product by ID.");

        group.MapPost("/products", CreateProductAsync)
            .WithName("CreateProduct")
            .WithDescription("Creates a new product.");

        // Product image endpoints
        group.MapPost("/products/{productId:int}/images", AddProductImageAsync)
            .WithName("AddProductImage")
            .WithDescription("Adds an image to a product with optional server-side CLIP embedding generation.");

        group.MapPost("/products/{productId:int}/images/embedding", AddProductImageWithEmbeddingAsync)
            .WithName("AddProductImageWithEmbedding")
            .WithDescription("Adds an image to a product with a pre-computed embedding.");
    }

    // ========== Provider Endpoints ==========

    private static async Task<IResult> GetProvidersAsync(
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providers = await dbContext.Providers
            .Select(p => new ProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                LogoUrl = p.LogoUrl,
                WebsiteUrl = p.WebsiteUrl,
                ProductCount = p.Products.Count
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(providers);
    }

    private static async Task<IResult> CreateProviderAsync(
        [FromBody] CreateProviderRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var provider = new Provider
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl,
            WebsiteUrl = request.WebsiteUrl
        };

        dbContext.Providers.Add(provider);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/providers/{provider.Id}", new ProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            LogoUrl = provider.LogoUrl,
            WebsiteUrl = provider.WebsiteUrl,
            ProductCount = 0
        });
    }

    // ========== Product Endpoints ==========

    private static async Task<IResult> GetProductsAsync(
        VisualSearchDbContext dbContext,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? providerId = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products
            .Include(p => p.Provider)
            .Include(p => p.Images)
            .AsQueryable();

        if (providerId.HasValue)
        {
            query = query.Where(p => p.ProviderId == providerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                ProviderId = p.ProviderId,
                ProviderName = p.Provider!.Name,
                ExternalId = p.ExternalId,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Currency = p.Currency,
                Category = p.Category,
                ProductUrl = p.ProductUrl,
                Images = p.Images.Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary,
                    HasEmbedding = i.Embedding != null
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<ProductDto>
        {
            Items = products,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    private static async Task<IResult> GetProductByIdAsync(
        int id,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(p => p.Provider)
            .Include(p => p.Images)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                ProviderId = p.ProviderId,
                ProviderName = p.Provider!.Name,
                ExternalId = p.ExternalId,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Currency = p.Currency,
                Category = p.Category,
                ProductUrl = p.ProductUrl,
                Images = p.Images.Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary,
                    HasEmbedding = i.Embedding != null
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return product is not null
            ? Results.Ok(product)
            : Results.NotFound();
    }

    private static async Task<IResult> CreateProductAsync(
        [FromBody] CreateProductRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Verify provider exists
        var providerExists = await dbContext.Providers
            .AnyAsync(p => p.Id == request.ProviderId, cancellationToken);

        if (!providerExists)
        {
            return Results.BadRequest(new { Error = $"Provider with ID {request.ProviderId} not found." });
        }

        var product = new Product
        {
            ProviderId = request.ProviderId,
            ExternalId = request.ExternalId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency ?? "EUR",
            Category = request.Category,
            ProductUrl = request.ProductUrl
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/products/{product.Id}", new { product.Id });
    }

    // ========== Product Image Endpoints ==========

    private static async Task<IResult> AddProductImageAsync(
        int productId,
        [FromBody] AddImageRequest request,
        VisualSearchDbContext dbContext,
        ClipEmbeddingService clipService,
        IHttpClientFactory httpClientFactory,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Verify product exists
        var product = await dbContext.Products.FindAsync([productId], cancellationToken);
        if (product is null)
        {
            return Results.NotFound(new { Error = $"Product with ID {productId} not found." });
        }

        float[]? embedding = null;

        // Try to generate embedding server-side if model is available
        if (clipService.IsModelLoaded && !string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            using var httpClient = httpClientFactory.CreateClient();
            embedding = await clipService.GenerateEmbeddingFromUrlAsync(request.ImageUrl, httpClient, cancellationToken);

            if (embedding is null)
            {
                logger.LogWarning("Failed to generate embedding for image URL: {ImageUrl}", request.ImageUrl);
            }
        }

        var productImage = new ProductImage
        {
            ProductId = productId,
            ImageUrl = request.ImageUrl,
            Embedding = embedding is not null ? new Vector(embedding) : null,
            IsPrimary = request.IsPrimary
        };

        // If this is primary, unset other primary images
        if (request.IsPrimary)
        {
            await dbContext.ProductImages
                .Where(pi => pi.ProductId == productId && pi.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(pi => pi.IsPrimary, false), cancellationToken);
        }

        dbContext.ProductImages.Add(productImage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/products/{productId}/images/{productImage.Id}", new
        {
            productImage.Id,
            productImage.ImageUrl,
            productImage.IsPrimary,
            HasEmbedding = productImage.Embedding is not null
        });
    }

    private static async Task<IResult> AddProductImageWithEmbeddingAsync(
        int productId,
        [FromBody] AddImageWithEmbeddingRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Verify product exists
        var product = await dbContext.Products.FindAsync([productId], cancellationToken);
        if (product is null)
        {
            return Results.NotFound(new { Error = $"Product with ID {productId} not found." });
        }

        // Validate embedding dimension
        if (request.Embedding is null || request.Embedding.Length != 512)
        {
            return Results.BadRequest(new { Error = "Embedding must be a 512-dimensional vector." });
        }

        var productImage = new ProductImage
        {
            ProductId = productId,
            ImageUrl = request.ImageUrl,
            Embedding = new Vector(request.Embedding),
            IsPrimary = request.IsPrimary
        };

        // If this is primary, unset other primary images
        if (request.IsPrimary)
        {
            await dbContext.ProductImages
                .Where(pi => pi.ProductId == productId && pi.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(pi => pi.IsPrimary, false), cancellationToken);
        }

        dbContext.ProductImages.Add(productImage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/products/{productId}/images/{productImage.Id}", new
        {
            productImage.Id,
            productImage.ImageUrl,
            productImage.IsPrimary,
            HasEmbedding = true
        });
    }

    // ========== DTOs ==========

    private sealed class ProviderDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public int ProductCount { get; set; }
    }

    private sealed class ProductDto
    {
        public int Id { get; set; }
        public int ProviderId { get; set; }
        public required string ProviderName { get; set; }
        public string? ExternalId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? Category { get; set; }
        public string? ProductUrl { get; set; }
        public List<ProductImageDto> Images { get; set; } = [];
    }

    private sealed class ProductImageDto
    {
        public int Id { get; set; }
        public required string ImageUrl { get; set; }
        public bool IsPrimary { get; set; }
        public bool HasEmbedding { get; set; }
    }

    private sealed class PagedResult<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    private sealed class CreateProviderRequest
    {
        public required string Name { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
    }

    private sealed class CreateProductRequest
    {
        public int ProviderId { get; set; }
        public string? ExternalId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? Category { get; set; }
        public string? ProductUrl { get; set; }
    }

    private sealed class AddImageRequest
    {
        public required string ImageUrl { get; set; }
        public bool IsPrimary { get; set; }
    }

    private sealed class AddImageWithEmbeddingRequest
    {
        public required string ImageUrl { get; set; }
        public bool IsPrimary { get; set; }
        public float[]? Embedding { get; set; }
    }
}
