using Microsoft.AspNetCore.Mvc;
using VisualSearch.Api.Application.Services;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Admin endpoints for managing providers, products, and product images.
/// Includes full CRUD operations with auto-vectorization on image changes.
/// All operations use Application Services following Clean Architecture.
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

        // User endpoints
        group.MapGet("/users", GetUsersAsync)
            .WithName("GetUsers")
            .WithDescription("Gets all admin users.");

        group.MapPost("/users", CreateUserAsync)
            .WithName("CreateUser")
            .WithDescription("Creates a new admin user.");

        group.MapDelete("/users/{id:int}", DeleteUserAsync)
            .WithName("DeleteUser")
            .WithDescription("Deletes an admin user.");

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

        // Category endpoints
        group.MapGet("/categories", GetCategoriesAsync)
            .WithName("GetCategories")
            .WithDescription("Gets all categories.");

        group.MapGet("/categories/{id:int}", GetCategoryByIdAsync)
            .WithName("GetCategoryById")
            .WithDescription("Gets a category by ID.");

        group.MapPost("/categories", CreateCategoryAsync)
            .WithName("CreateCategory")
            .WithDescription("Creates a new category.");

        group.MapPut("/categories/{id:int}", UpdateCategoryAsync)
            .WithName("UpdateCategory")
            .WithDescription("Updates a category.");

        group.MapDelete("/categories/{id:int}", DeleteCategoryAsync)
            .WithName("DeleteCategory")
            .WithDescription("Deletes a category. Fails if products are associated.");

        group.MapPatch("/categories/{id:int}/detection", ToggleCategoryDetectionAsync)
            .WithName("ToggleCategoryDetection")
            .WithDescription("Toggles detection enabled for a category.");

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
        DashboardService dashboardService,
        CancellationToken cancellationToken)
    {
        var stats = await dashboardService.GetAdminStatsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    // ========== System Status ==========

    private static IResult GetSystemStatusAsync(DashboardService dashboardService)
    {
        var status = dashboardService.GetAdminSystemStatus();
        return Results.Ok(status);
    }

    // ========== Provider Endpoints ==========

    private static async Task<IResult> GetProvidersAsync(
        ProviderService providerService,
        CancellationToken cancellationToken)
    {
        var providers = await providerService.GetAllAdminProvidersAsync(cancellationToken);
        return Results.Ok(providers);
    }

    private static async Task<IResult> GetProviderByIdAsync(
        int id,
        ProviderService providerService,
        CancellationToken cancellationToken)
    {
        var provider = await providerService.GetAdminProviderByIdAsync(id, cancellationToken);
        return provider is not null ? Results.Ok(provider) : Results.NotFound();
    }

    private static async Task<IResult> CreateProviderAsync(
        [FromBody] CreateProviderRequest request,
        ProviderService providerService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Provider name is required." });
        }

        try
        {
            var provider = await providerService.CreateAdminProviderAsync(request, cancellationToken);
            return Results.Created($"/api/admin/providers/{provider.Id}", provider);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateProviderAsync(
        int id,
        [FromBody] UpdateProviderRequest request,
        ProviderService providerService,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = await providerService.UpdateAdminProviderAsync(
                id, request.Name, request.LogoUrl, request.WebsiteUrl, cancellationToken);

            return provider is not null ? Results.Ok(provider) : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteProviderAsync(
        int id,
        ProviderService providerService,
        CancellationToken cancellationToken)
    {
        var deleted = await providerService.DeleteProviderCascadeAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // ========== Category Endpoints ==========

    private static async Task<IResult> GetCategoriesAsync(
        CategoryService categoryService,
        [FromQuery] bool? detectionEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryService.GetAllAdminCategoriesAsync(detectionEnabled, cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetCategoryByIdAsync(
        int id,
        CategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.GetAdminCategoryByIdAsync(id, cancellationToken);
        return category is not null ? Results.Ok(category) : Results.NotFound();
    }

    private static async Task<IResult> CreateCategoryAsync(
        [FromBody] CreateCategoryRequest request,
        CategoryService categoryService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Category name is required." });
        }

        try
        {
            var category = await categoryService.CreateAdminCategoryAsync(request, cancellationToken);
            return Results.Created($"/api/admin/categories/{category.Id}", category);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateCategoryAsync(
        int id,
        [FromBody] UpdateCategoryRequest request,
        CategoryService categoryService,
        CancellationToken cancellationToken)
    {
        try
        {
            var category = await categoryService.UpdateAdminCategoryAsync(
                id, request.Name, request.CocoClassId, request.DetectionEnabled, cancellationToken);

            return category is not null ? Results.Ok(category) : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteCategoryAsync(
        int id,
        CategoryService categoryService,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await categoryService.DeleteAdminCategoryAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> ToggleCategoryDetectionAsync(
        int id,
        [FromBody] ToggleDetectionRequest request,
        CategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.ToggleAdminDetectionAsync(id, request.DetectionEnabled, cancellationToken);
        return category is not null ? Results.Ok(category) : Results.NotFound();
    }

    // ========== Product Endpoints ==========

    private static async Task<IResult> GetProductsAsync(
        ProductService productService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? providerId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await productService.GetAdminProductsPagedAsync(
            page, pageSize, providerId, categoryId, search, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProductByIdAsync(
        int id,
        ProductService productService,
        CancellationToken cancellationToken)
    {
        var product = await productService.GetAdminProductByIdAsync(id, cancellationToken);
        return product is not null ? Results.Ok(product) : Results.NotFound();
    }

    private static async Task<IResult> CreateProductAsync(
        [FromBody] CreateProductRequestAdmin request,
        ProductService productService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Product name is required." });
        }

        try
        {
            var productId = await productService.CreateAdminProductAsync(
                request.ProviderId,
                request.Name,
                request.ExternalId,
                request.Description,
                request.Price,
                request.Currency,
                request.CategoryId,
                request.ProductUrl,
                cancellationToken);

            return Results.Created($"/api/admin/products/{productId}", new { Id = productId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateProductAsync(
        int id,
        [FromBody] UpdateProductRequestAdmin request,
        ProductService productService,
        CancellationToken cancellationToken)
    {
        try
        {
            var productId = await productService.UpdateAdminProductAsync(
                id,
                request.ProviderId,
                request.ExternalId,
                request.Name,
                request.Description,
                request.Price,
                request.Currency,
                request.CategoryId,
                request.ProductUrl,
                cancellationToken);

            return productId.HasValue ? Results.Ok(new { Id = productId.Value }) : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteProductAsync(
        int id,
        ProductService productService,
        CancellationToken cancellationToken)
    {
        var deleted = await productService.DeleteAdminProductAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // ========== Product Image Endpoints ==========

    private static async Task<IResult> AddProductImageAsync(
        int productId,
        [FromBody] AddImageRequest request,
        IProductImageService productImageService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return Results.BadRequest(new { Error = "Image URL is required." });
        }

        try
        {
            var image = await productImageService.AddFromUrlWithVectorizationAsync(
                productId, request.ImageUrl, request.IsPrimary, cancellationToken);

            if (!image.HasEmbedding)
            {
                logger.LogWarning("Failed to generate embedding for image URL: {ImageUrl}", request.ImageUrl);
            }

            return Results.Created($"/api/admin/products/{productId}/images/{image.Id}", image);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateProductImageAsync(
        int productId,
        int imageId,
        [FromBody] UpdateImageRequest request,
        IProductImageService productImageService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var image = await productImageService.UpdateWithUrlChangeAsync(
            productId, imageId, request.ImageUrl, request.IsPrimary, cancellationToken);

        if (image is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(image);
    }

    private static async Task<IResult> DeleteProductImageAsync(
        int productId,
        int imageId,
        IProductImageService productImageService,
        CancellationToken cancellationToken)
    {
        var deleted = await productImageService.DeleteByProductAsync(productId, imageId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // ========== Image Upload Endpoint ==========

    private static async Task<IResult> UploadProductImageAsync(
        int productId,
        IFormFile file,
        [FromForm] bool isPrimary,
        IProductImageService productImageService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
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
            using var stream = file.OpenReadStream();
            var image = await productImageService.UploadWithVectorizationAsync(
                productId, stream, file.FileName, isPrimary, cancellationToken);

            if (!image.HasEmbedding)
            {
                logger.LogWarning("Failed to generate embedding for uploaded image");
            }

            logger.LogInformation("Uploaded image for product {ProductId}: {LocalPath}", productId, image.LocalPath);

            return Results.Created($"/api/admin/products/{productId}/images/{image.Id}", image);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image for product {ProductId}", productId);
            return Results.Problem("Failed to process uploaded image.");
        }
    }

    // ========== Image Download from URL Endpoint ==========

    private static async Task<IResult> DownloadAndSaveProductImageAsync(
        int productId,
        [FromBody] DownloadImageRequest request,
        IProductImageService productImageService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
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
            var image = await productImageService.DownloadAndSaveWithVectorizationAsync(
                productId, request.ImageUrl, request.IsPrimary, cancellationToken);

            if (!image.HasEmbedding)
            {
                logger.LogWarning("Failed to generate embedding for downloaded image from {Url}", request.ImageUrl);
            }

            logger.LogInformation("Downloaded and saved image for product {ProductId} from {Url}: {LocalPath}",
                productId, request.ImageUrl, image.LocalPath);

            return Results.Created($"/api/admin/products/{productId}/images/{image.Id}", image);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound(new { Error = ex.Message });
            }
            return Results.BadRequest(new { Error = ex.Message });
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
        IProductImageService productImageService,
        ProductService productService,
        CancellationToken cancellationToken)
    {
        if (!productImageService.IsVectorizationAvailable)
        {
            return Results.BadRequest(new { Error = "Vectorization is not available. CLIP model not loaded." });
        }

        var product = await productService.GetAdminProductByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        var successCount = await productImageService.VectorizeProductImagesAsync(productId, cancellationToken);

        return Results.Ok(new ProductVectorizationResultDto(
            ProductId: productId,
            TotalImages: product.Images.Count,
            VectorizedImages: successCount
        ));
    }

    private static async Task<IResult> VectorizeAllAsync(
        [FromQuery] bool force = false,
        IProductImageService productImageService = null!,
        CancellationToken cancellationToken = default)
    {
        if (!productImageService.IsVectorizationAvailable)
        {
            return Results.BadRequest(new { Error = "Vectorization is not available. CLIP model not loaded." });
        }

        var result = await productImageService.VectorizeAllAdminAsync(force, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUsersAsync(
        [FromServices] AuthService authService,
        CancellationToken cancellationToken)
    {
        var users = await authService.GetAllUsersAsync(cancellationToken);
        return Results.Ok(users);
    }

    private static async Task<IResult> CreateUserAsync(
        [FromBody] CreateAdminUserDto request,
        [FromServices] AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.CreateUserAsync(request, cancellationToken);
            return Results.Created($"/api/admin/users/{user.Id}", user);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteUserAsync(
        int id,
        [FromServices] AuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.DeleteUserAsync(id, cancellationToken);
        return Results.NoContent();
    }

    // ========== Request DTOs (Admin-specific with ExternalId) ==========

    private sealed class CreateProductRequestAdmin
    {
        public int ProviderId { get; set; }
        public string? ExternalId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public int? CategoryId { get; set; }
        public string? ProductUrl { get; set; }
    }

    private sealed class UpdateProductRequestAdmin
    {
        public int? ProviderId { get; set; }
        public string? ExternalId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? Currency { get; set; }
        public int? CategoryId { get; set; }
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

    /// <summary>
    /// Request DTO for downloading an image from URL.
    /// </summary>
    private sealed record DownloadImageRequest(string ImageUrl, bool IsPrimary = false);
}
