using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Admin endpoints for managing providers, products, and product images.
/// Includes full CRUD operations with auto-vectorization on image changes.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>
    /// Maps the admin endpoints to the application.
    /// </summary>
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        // Dashboard stats
        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetStats")
            .WithDescription("Gets dashboard statistics.");

        // Provider endpoints
        group.MapGet("/providers", GetProvidersAsync)
            .WithName("GetProviders")
            .WithDescription("Gets all providers.");

        group.MapGet("/providers/{id:int}", GetProviderByIdAsync)
            .WithName("GetProviderById")
            .WithDescription("Gets a provider by ID.");

        group.MapPost("/providers", CreateProviderAsync)
            .WithName("CreateProvider")
            .WithDescription("Creates a new provider.");

        group.MapPut("/providers/{id:int}", UpdateProviderAsync)
            .WithName("UpdateProvider")
            .WithDescription("Updates a provider.");

        group.MapDelete("/providers/{id:int}", DeleteProviderAsync)
            .WithName("DeleteProvider")
            .WithDescription("Deletes a provider and all its products.");

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

        group.MapPut("/products/{id:int}", UpdateProductAsync)
            .WithName("UpdateProduct")
            .WithDescription("Updates a product.");

        group.MapDelete("/products/{id:int}", DeleteProductAsync)
            .WithName("DeleteProduct")
            .WithDescription("Deletes a product and all its images.");

        // Product image endpoints
        group.MapPost("/products/{productId:int}/images", AddProductImageAsync)
            .WithName("AddProductImage")
            .WithDescription("Adds an image to a product with automatic CLIP embedding generation.");

        group.MapPut("/products/{productId:int}/images/{imageId:int}", UpdateProductImageAsync)
            .WithName("UpdateProductImage")
            .WithDescription("Updates a product image. Triggers re-vectorization if URL changes.");

        group.MapDelete("/products/{productId:int}/images/{imageId:int}", DeleteProductImageAsync)
            .WithName("DeleteProductImage")
            .WithDescription("Deletes a product image.");

        // Image upload endpoint
        group.MapPost("/products/{productId:int}/images/upload", UploadProductImageAsync)
            .WithName("UploadProductImage")
            .WithDescription("Uploads an image file to a product with automatic resize, compression, and CLIP embedding generation.")
            .DisableAntiforgery();

        // Image download from URL endpoint (server-side download to avoid CORS)
        group.MapPost("/products/{productId:int}/images/download", DownloadAndSaveProductImageAsync)
            .WithName("DownloadProductImage")
            .WithDescription("Downloads an image from a URL and saves it locally with automatic resize, compression, and CLIP embedding generation.");

        // Vectorization endpoints
        group.MapPost("/products/{productId:int}/vectorize", VectorizeProductAsync)
            .WithName("VectorizeProduct")
            .WithDescription("Regenerates CLIP embeddings for all images of a product.");

        group.MapPost("/vectorize-all", VectorizeAllAsync)
            .WithName("VectorizeAll")
            .WithDescription("Regenerates CLIP embeddings for all product images.");

        // System info
        group.MapGet("/system-status", GetSystemStatusAsync)
            .WithName("GetSystemStatus")
            .WithDescription("Gets AI model loading status and system info.");
    }

    // ========== Dashboard Stats ==========

    private static async Task<IResult> GetStatsAsync(
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var productsCount = await dbContext.Products.CountAsync(cancellationToken);
        var providersCount = await dbContext.Providers.CountAsync(cancellationToken);
        var imagesCount = await dbContext.ProductImages.CountAsync(cancellationToken);
        var vectorizedCount = await dbContext.ProductImages.CountAsync(pi => pi.Embedding != null, cancellationToken);

        return Results.Ok(new
        {
            Products = productsCount,
            Providers = providersCount,
            Images = imagesCount,
            VectorizedImages = vectorizedCount,
            VectorizationProgress = imagesCount > 0 ? (double)vectorizedCount / imagesCount * 100 : 100
        });
    }

    // ========== System Status ==========

    private static IResult GetSystemStatusAsync(
        ClipEmbeddingService clipService,
        ObjectDetectionService detectionService,
        VectorizationService vectorizationService)
    {
        return Results.Ok(new
        {
            ClipModelLoaded = clipService.IsModelLoaded,
            YoloModelLoaded = detectionService.IsModelLoaded,
            VectorizationAvailable = vectorizationService.IsAvailable,
            ObjectDetectionAvailable = vectorizationService.IsDetectionAvailable
        });
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

    private static async Task<IResult> GetProviderByIdAsync(
        int id,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.Providers
            .Where(p => p.Id == id)
            .Select(p => new ProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                LogoUrl = p.LogoUrl,
                WebsiteUrl = p.WebsiteUrl,
                ProductCount = p.Products.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        return provider is not null ? Results.Ok(provider) : Results.NotFound();
    }

    private static async Task<IResult> CreateProviderAsync(
        [FromBody] CreateProviderRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Provider name is required." });
        }

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

    private static async Task<IResult> UpdateProviderAsync(
        int id,
        [FromBody] UpdateProviderRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.Providers.FindAsync([id], cancellationToken);

        if (provider is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            provider.Name = request.Name;
        }

        if (request.LogoUrl is not null)
        {
            provider.LogoUrl = request.LogoUrl;
        }

        if (request.WebsiteUrl is not null)
        {
            provider.WebsiteUrl = request.WebsiteUrl;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            LogoUrl = provider.LogoUrl,
            WebsiteUrl = provider.WebsiteUrl,
            ProductCount = await dbContext.Products.CountAsync(p => p.ProviderId == id, cancellationToken)
        });
    }

    private static async Task<IResult> DeleteProviderAsync(
        int id,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.Providers
            .Include(p => p.Products)
                .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (provider is null)
        {
            return Results.NotFound();
        }

        // Delete all product images first
        foreach (var product in provider.Products)
        {
            dbContext.ProductImages.RemoveRange(product.Images);
        }

        // Delete all products
        dbContext.Products.RemoveRange(provider.Products);

        // Delete provider
        dbContext.Providers.Remove(provider);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // ========== Product Endpoints ==========

    private static async Task<IResult> GetProductsAsync(
        VisualSearchDbContext dbContext,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? providerId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
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
                CreatedAt = p.CreatedAt,
                Images = p.Images.Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    LocalPath = i.LocalPath,
                    IsLocalFile = i.LocalPath != null,
                    IsPrimary = i.IsPrimary,
                    HasEmbedding = i.Embedding != null,
                    CreatedAt = i.CreatedAt
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
                CreatedAt = p.CreatedAt,
                Images = p.Images.Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    LocalPath = i.LocalPath,
                    IsLocalFile = i.LocalPath != null,
                    IsPrimary = i.IsPrimary,
                    HasEmbedding = i.Embedding != null,
                    CreatedAt = i.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return product is not null ? Results.Ok(product) : Results.NotFound();
    }

    private static async Task<IResult> CreateProductAsync(
        [FromBody] CreateProductRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Product name is required." });
        }

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

    private static async Task<IResult> UpdateProductAsync(
        int id,
        [FromBody] UpdateProductRequest request,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync([id], cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        if (request.ProviderId.HasValue)
        {
            var providerExists = await dbContext.Providers
                .AnyAsync(p => p.Id == request.ProviderId.Value, cancellationToken);

            if (!providerExists)
            {
                return Results.BadRequest(new { Error = $"Provider with ID {request.ProviderId.Value} not found." });
            }

            product.ProviderId = request.ProviderId.Value;
        }

        if (request.ExternalId is not null)
        {
            product.ExternalId = request.ExternalId;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            product.Name = request.Name;
        }

        if (request.Description is not null)
        {
            product.Description = request.Description;
        }

        if (request.Price.HasValue)
        {
            product.Price = request.Price.Value;
        }

        if (request.Currency is not null)
        {
            product.Currency = request.Currency;
        }

        if (request.Category is not null)
        {
            product.Category = request.Category;
        }

        if (request.ProductUrl is not null)
        {
            product.ProductUrl = request.ProductUrl;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { product.Id });
    }

    private static async Task<IResult> DeleteProductAsync(
        int id,
        VisualSearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        // Delete all images first
        dbContext.ProductImages.RemoveRange(product.Images);

        // Delete product
        dbContext.Products.Remove(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // ========== Product Image Endpoints ==========

    private static async Task<IResult> AddProductImageAsync(
        int productId,
        [FromBody] AddImageRequest request,
        VisualSearchDbContext dbContext,
        VectorizationService vectorizationService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return Results.BadRequest(new { Error = "Image URL is required." });
        }

        var product = await dbContext.Products.FindAsync([productId], cancellationToken);

        if (product is null)
        {
            return Results.NotFound(new { Error = $"Product with ID {productId} not found." });
        }

        // Generate embedding
        float[]? embedding = null;
        if (vectorizationService.IsAvailable)
        {
            embedding = await vectorizationService.GenerateEmbeddingFromUrlAsync(request.ImageUrl, cancellationToken);

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

        return Results.Created($"/api/admin/products/{productId}/images/{productImage.Id}", new ProductImageDto
        {
            Id = productImage.Id,
            ImageUrl = productImage.ImageUrl,
            LocalPath = productImage.LocalPath,
            IsLocalFile = productImage.LocalPath is not null,
            IsPrimary = productImage.IsPrimary,
            HasEmbedding = productImage.Embedding is not null,
            CreatedAt = productImage.CreatedAt
        });
    }

    private static async Task<IResult> UpdateProductImageAsync(
        int productId,
        int imageId,
        [FromBody] UpdateImageRequest request,
        VisualSearchDbContext dbContext,
        VectorizationService vectorizationService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var productImage = await dbContext.ProductImages
            .FirstOrDefaultAsync(pi => pi.Id == imageId && pi.ProductId == productId, cancellationToken);

        if (productImage is null)
        {
            return Results.NotFound();
        }

        var urlChanged = false;

        if (!string.IsNullOrWhiteSpace(request.ImageUrl) && request.ImageUrl != productImage.ImageUrl)
        {
            productImage.ImageUrl = request.ImageUrl;
            urlChanged = true;
        }

        if (request.IsPrimary.HasValue)
        {
            if (request.IsPrimary.Value && !productImage.IsPrimary)
            {
                // Unset other primary images
                await dbContext.ProductImages
                    .Where(pi => pi.ProductId == productId && pi.IsPrimary && pi.Id != imageId)
                    .ExecuteUpdateAsync(s => s.SetProperty(pi => pi.IsPrimary, false), cancellationToken);
            }

            productImage.IsPrimary = request.IsPrimary.Value;
        }

        // Re-vectorize if URL changed
        if (urlChanged && vectorizationService.IsAvailable)
        {
            var embedding = await vectorizationService.GenerateEmbeddingFromUrlAsync(productImage.ImageUrl, cancellationToken);

            if (embedding is not null)
            {
                productImage.Embedding = new Vector(embedding);
                logger.LogInformation("Re-vectorized image {ImageId} after URL change", imageId);
            }
            else
            {
                logger.LogWarning("Failed to re-vectorize image {ImageId}", imageId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ProductImageDto
        {
            Id = productImage.Id,
            ImageUrl = productImage.ImageUrl,
            LocalPath = productImage.LocalPath,
            IsLocalFile = productImage.LocalPath is not null,
            IsPrimary = productImage.IsPrimary,
            HasEmbedding = productImage.Embedding is not null,
            CreatedAt = productImage.CreatedAt
        });
    }

    private static async Task<IResult> DeleteProductImageAsync(
        int productId,
        int imageId,
        VisualSearchDbContext dbContext,
        ImageUploadService imageUploadService,
        CancellationToken cancellationToken)
    {
        var productImage = await dbContext.ProductImages
            .FirstOrDefaultAsync(pi => pi.Id == imageId && pi.ProductId == productId, cancellationToken);

        if (productImage is null)
        {
            return Results.NotFound();
        }

        // Delete local file if exists
        if (!string.IsNullOrWhiteSpace(productImage.LocalPath))
        {
            imageUploadService.DeleteImage(productImage.LocalPath);
        }

        dbContext.ProductImages.Remove(productImage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // ========== Image Upload Endpoint ==========

    private static async Task<IResult> UploadProductImageAsync(
        int productId,
        IFormFile file,
        [FromForm] bool isPrimary,
        VisualSearchDbContext dbContext,
        ImageUploadService imageUploadService,
        VectorizationService vectorizationService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Validate product exists
        var product = await dbContext.Products.FindAsync([productId], cancellationToken);
        if (product is null)
        {
            return Results.NotFound(new { Error = $"Product with ID {productId} not found." });
        }

        // Validate file
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { Error = "No file uploaded." });
        }

        // Validate content type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return Results.BadRequest(new { Error = "Invalid file type. Allowed: JPEG, PNG, GIF, WebP." });
        }

        // Validate file size (max 20MB)
        if (file.Length > 20 * 1024 * 1024)
        {
            return Results.BadRequest(new { Error = "File too large. Maximum size is 20MB." });
        }

        try
        {
            // Save and process the image
            using var stream = file.OpenReadStream();
            var (relativePath, imageBytes) = await imageUploadService.SaveImageAsync(stream, file.FileName, cancellationToken);

            // Build the URL for accessing the image
            var imageUrl = $"/uploads/{relativePath}";

            // Generate embedding
            float[]? embedding = null;
            if (vectorizationService.IsAvailable)
            {
                embedding = await vectorizationService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
                if (embedding is null)
                {
                    logger.LogWarning("Failed to generate embedding for uploaded image");
                }
            }

            // Create product image record
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                LocalPath = relativePath,
                Embedding = embedding is not null ? new Pgvector.Vector(embedding) : null,
                IsPrimary = isPrimary
            };

            // If this is primary, unset other primary images
            if (isPrimary)
            {
                await dbContext.ProductImages
                    .Where(pi => pi.ProductId == productId && pi.IsPrimary)
                    .ExecuteUpdateAsync(s => s.SetProperty(pi => pi.IsPrimary, false), cancellationToken);
            }

            dbContext.ProductImages.Add(productImage);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Uploaded image for product {ProductId}: {LocalPath}", productId, relativePath);

            return Results.Created($"/api/admin/products/{productId}/images/{productImage.Id}", new ProductImageDto
            {
                Id = productImage.Id,
                ImageUrl = productImage.ImageUrl,
                LocalPath = productImage.LocalPath,
                IsLocalFile = true,
                IsPrimary = productImage.IsPrimary,
                HasEmbedding = productImage.Embedding is not null,
                CreatedAt = productImage.CreatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image for product {ProductId}", productId);
            return Results.Problem("Failed to process uploaded image.");
        }
    }

    // ========== Image Download from URL Endpoint ==========

    /// <summary>
    /// Request DTO for downloading an image from URL.
    /// </summary>
    private sealed record DownloadImageRequest(string ImageUrl, bool IsPrimary = false);

    private static async Task<IResult> DownloadAndSaveProductImageAsync(
        int productId,
        [FromBody] DownloadImageRequest request,
        VisualSearchDbContext dbContext,
        ImageUploadService imageUploadService,
        VectorizationService vectorizationService,
        IHttpClientFactory httpClientFactory,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Validate product exists
        var product = await dbContext.Products.FindAsync([productId], cancellationToken);
        if (product is null)
        {
            return Results.NotFound(new { Error = $"Product with ID {productId} not found." });
        }

        // Validate URL
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return Results.BadRequest(new { Error = "Image URL is required." });
        }

        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return Results.BadRequest(new { Error = "Invalid image URL." });
        }

        try
        {
            // Download the image from URL (bypass SSL validation for problematic URLs)
            using var httpClient = httpClientFactory.CreateClient("ImageDownload");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Validate content type
            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/jpg" };
            if (contentType is null || !allowedTypes.Contains(contentType))
            {
                return Results.BadRequest(new { Error = $"Invalid content type: {contentType}. Expected an image." });
            }

            // Read image bytes
            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            // Validate size (max 20MB)
            if (imageBytes.Length > 20 * 1024 * 1024)
            {
                return Results.BadRequest(new { Error = "Downloaded image too large. Maximum size is 20MB." });
            }

            // Generate filename from URL
            var filename = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(filename) || !filename.Contains('.'))
            {
                var extension = contentType?.Replace("image/", "") ?? "jpg";
                if (extension == "jpeg") extension = "jpg";
                filename = $"downloaded-{Guid.NewGuid():N}.{extension}";
            }

            // Save and process the image
            var (relativePath, processedBytes) = await imageUploadService.SaveImageBytesAsync(imageBytes, filename, cancellationToken);

            // Build the URL for accessing the image
            var localImageUrl = $"/uploads/{relativePath}";

            // Generate embedding
            float[]? embedding = null;
            if (vectorizationService.IsAvailable)
            {
                embedding = await vectorizationService.GenerateEmbeddingAsync(processedBytes, cancellationToken);
                if (embedding is null)
                {
                    logger.LogWarning("Failed to generate embedding for downloaded image from {Url}", request.ImageUrl);
                }
            }

            // Create product image record
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = localImageUrl,
                LocalPath = relativePath,
                Embedding = embedding is not null ? new Pgvector.Vector(embedding) : null,
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

            logger.LogInformation("Downloaded and saved image for product {ProductId} from {Url}: {LocalPath}", 
                productId, request.ImageUrl, relativePath);

            return Results.Created($"/api/admin/products/{productId}/images/{productImage.Id}", new ProductImageDto
            {
                Id = productImage.Id,
                ImageUrl = productImage.ImageUrl,
                LocalPath = productImage.LocalPath,
                IsLocalFile = true,
                IsPrimary = productImage.IsPrimary,
                HasEmbedding = productImage.Embedding is not null,
                CreatedAt = productImage.CreatedAt
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to download image from {Url}", request.ImageUrl);
            return Results.BadRequest(new { Error = $"Failed to download image: {ex.Message}" });
        }
        catch (TaskCanceledException)
        {
            return Results.BadRequest(new { Error = "Image download timed out." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process downloaded image from {Url}", request.ImageUrl);
            return Results.Problem("Failed to process downloaded image.");
        }
    }

    // ========== Vectorization Endpoints ==========

    private static async Task<IResult> VectorizeProductAsync(
        int productId,
        VisualSearchDbContext dbContext,
        VectorizationService vectorizationService,
        CancellationToken cancellationToken)
    {
        if (!vectorizationService.IsAvailable)
        {
            return Results.BadRequest(new { Error = "Vectorization is not available. CLIP model not loaded." });
        }

        var product = await dbContext.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        var successCount = await vectorizationService.VectorizeProductAsync(dbContext, productId, cancellationToken);

        return Results.Ok(new
        {
            ProductId = productId,
            TotalImages = product.Images.Count,
            VectorizedImages = successCount
        });
    }

    private static async Task<IResult> VectorizeAllAsync(
        [FromQuery] bool force = false,
        VisualSearchDbContext dbContext = null!,
        VectorizationService vectorizationService = null!,
        CancellationToken cancellationToken = default)
    {
        if (!vectorizationService.IsAvailable)
        {
            return Results.BadRequest(new { Error = "Vectorization is not available. CLIP model not loaded." });
        }

        var (success, total) = await vectorizationService.VectorizeAllAsync(dbContext, force, cancellationToken);

        return Results.Ok(new
        {
            TotalImages = total,
            VectorizedImages = success,
            SkippedOrFailed = total - success
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
        public DateTime CreatedAt { get; set; }
        public List<ProductImageDto> Images { get; set; } = [];
    }

    private sealed class ProductImageDto
    {
        public int Id { get; set; }
        public required string ImageUrl { get; set; }
        public string? LocalPath { get; set; }
        public bool IsLocalFile { get; set; }
        public bool IsPrimary { get; set; }
        public bool HasEmbedding { get; set; }
        public DateTime CreatedAt { get; set; }
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

    private sealed class UpdateProviderRequest
    {
        public string? Name { get; set; }
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

    private sealed class UpdateProductRequest
    {
        public int? ProviderId { get; set; }
        public string? ExternalId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? Currency { get; set; }
        public string? Category { get; set; }
        public string? ProductUrl { get; set; }
    }

    private sealed class AddImageRequest
    {
        public required string ImageUrl { get; set; }
        public bool IsPrimary { get; set; }
    }

    private sealed class UpdateImageRequest
    {
        public string? ImageUrl { get; set; }
        public bool? IsPrimary { get; set; }
    }
}
