using System.Net;
using System.Net.Http.Json;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Tests.Fixtures;

namespace VisualSearch.Api.Tests.Integration;

/// <summary>
/// Integration tests for admin CRUD operations (providers, products, categories).
/// Tests the main flows for managing entities through the admin API.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdminCrudTests : IntegrationTestBase
{
    public AdminCrudTests(PostgresContainerFixture dbFixture) : base(dbFixture)
    {
    }

    #region Provider CRUD Tests

    [Fact]
    public async Task CreateProvider_WithValidData_ReturnsCreatedProvider()
    {
        // Arrange
        var provider = new
        {
            Name = "Test Provider",
            WebsiteUrl = "https://test-provider.com",
            Description = "Test provider description"
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/admin/providers", provider);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<AdminProviderDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Provider");
        result.WebsiteUrl.Should().Be("https://test-provider.com");
    }

    [Fact]
    public async Task GetProviders_ReturnsProvidersList()
    {
        // Arrange - Create a provider first
        await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "List Test Provider",
            WebsiteUrl = "https://list-test.com"
        });

        // Act
        var response = await AuthenticatedGetAsync("/api/admin/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await response.Content.ReadFromJsonAsync<List<AdminProviderDto>>();
        providers.Should().NotBeNull();
        providers.Should().Contain(p => p.Name == "List Test Provider");
    }

    [Fact]
    public async Task GetProviderById_WhenExists_ReturnsProvider()
    {
        // Arrange - Create a provider
        var createResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Get By Id Provider",
            WebsiteUrl = "https://getbyid.com"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        // Act
        var response = await AuthenticatedGetAsync($"/api/admin/providers/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var provider = await response.Content.ReadFromJsonAsync<AdminProviderDto>();
        provider.Should().NotBeNull();
        provider!.Id.Should().Be(created.Id);
        provider.Name.Should().Be("Get By Id Provider");
    }

    [Fact]
    public async Task GetProviderById_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var response = await AuthenticatedGetAsync("/api/admin/providers/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateProvider_WithValidData_ReturnsUpdatedProvider()
    {
        // Arrange - Create a provider
        var createResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Update Test Provider",
            WebsiteUrl = "https://updatetest.com"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var updateRequest = new
        {
            Name = "Updated Provider Name",
            WebsiteUrl = "https://updated.com",
            Description = "Updated description"
        };

        // Act
        var response = await AuthenticatedPutAsync($"/api/admin/providers/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<AdminProviderDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Provider Name");
        updated.WebsiteUrl.Should().Be("https://updated.com");
    }

    [Fact]
    public async Task DeleteProvider_WhenExists_ReturnsNoContent()
    {
        // Arrange - Create a provider
        var createResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Delete Test Provider",
            WebsiteUrl = "https://deletetest.com"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        // Act
        var response = await AuthenticatedDeleteAsync($"/api/admin/providers/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await AuthenticatedGetAsync($"/api/admin/providers/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Category CRUD Tests

    [Fact]
    public async Task CreateCategory_WithValidData_ReturnsCreatedCategory()
    {
        // Arrange
        var category = new
        {
            Name = "Test Category",
            Description = "Test category description",
            CocoClassId = 50001,
            DetectionEnabled = true
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/admin/categories", category);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<AdminCategoryDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Category");
    }

    [Fact]
    public async Task GetCategories_ReturnsCategoriesList()
    {
        // Arrange - Create a category first
        await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "List Test Category",
            CocoClassId = 50002
        });

        // Act
        var response = await AuthenticatedGetAsync("/api/admin/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<List<AdminCategoryDto>>();
        categories.Should().NotBeNull();
        categories.Should().Contain(c => c.Name == "List Test Category");
    }

    #endregion

    #region Product CRUD Tests

    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsCreatedProduct()
    {
        // Arrange - Create provider and category first
        var providerResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Product Test Provider",
            WebsiteUrl = "https://producttest.com"
        });
        var provider = await providerResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var categoryResponse = await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "Product Test Category",
            CocoClassId = 50003
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryDto>();

        var product = new
        {
            Name = "Test Product",
            ProviderId = provider!.Id,
            CategoryId = category!.Id,
            Price = 99.99m,
            Currency = "EUR",
            ProductUrl = "https://producttest.com/product/1"
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/admin/products", product);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        created.Should().NotBeNull();

        var getResponse = await AuthenticatedGetAsync($"/api/admin/products/{created!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await getResponse.Content.ReadFromJsonAsync<AdminProductDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Product");
        result.Price.Should().Be(99.99m);
    }

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsPagedResults()
    {
        // Arrange - Create provider, category and products
        var providerResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Pagination Test Provider",
            WebsiteUrl = "https://paginationtest.com"
        });
        var provider = await providerResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var categoryResponse = await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "Pagination Test Category",
            CocoClassId = 50004
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryDto>();

        // Create multiple products
        for (var i = 1; i <= 5; i++)
        {
            await AuthenticatedPostAsync("/api/admin/products", new
            {
                Name = $"Pagination Product {i}",
                ProviderId = provider!.Id,
                CategoryId = category!.Id,
                Price = i * 10.0m
            });
        }

        // Act
        var response = await AuthenticatedGetAsync("/api/admin/products?page=1&pageSize=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AdminPagedResult<AdminProductDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountLessOrEqualTo(3);
    }

    [Fact]
    public async Task GetProductById_WhenExists_ReturnsProduct()
    {
        // Arrange - Create provider, category and product
        var providerResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "GetById Test Provider",
            WebsiteUrl = "https://getbyidtest.com"
        });
        var provider = await providerResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var categoryResponse = await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "GetById Test Category",
            CocoClassId = 50005
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryDto>();

        var createResponse = await AuthenticatedPostAsync("/api/admin/products", new
        {
            Name = "Get By Id Product",
            ProviderId = provider!.Id,
            CategoryId = category!.Id,
            Price = 50.0m
        });
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        created.Should().NotBeNull();

        // Act
        var response = await AuthenticatedGetAsync($"/api/admin/products/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await response.Content.ReadFromJsonAsync<AdminProductDto>();
        product.Should().NotBeNull();
        product!.Name.Should().Be("Get By Id Product");
    }

    [Fact]
    public async Task UpdateProduct_WithValidData_ReturnsUpdatedProduct()
    {
        // Arrange - Create provider, category and product
        var providerResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Update Product Provider",
            WebsiteUrl = "https://updateproduct.com"
        });
        var provider = await providerResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var categoryResponse = await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "Update Product Category",
            CocoClassId = 50006
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryDto>();

        var createResponse = await AuthenticatedPostAsync("/api/admin/products", new
        {
            Name = "Original Product Name",
            ProviderId = provider!.Id,
            CategoryId = category!.Id,
            Price = 100.0m
        });
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        created.Should().NotBeNull();

        var updateRequest = new
        {
            Name = "Updated Product Name",
            ProviderId = provider.Id,
            CategoryId = category!.Id,
            Price = 150.0m,
            Currency = "EUR"
        };

        // Act
        var response = await AuthenticatedPutAsync($"/api/admin/products/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<IdResponse>();
        updated.Should().NotBeNull();

        var getResponse = await AuthenticatedGetAsync($"/api/admin/products/{updated!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await getResponse.Content.ReadFromJsonAsync<AdminProductDto>();
        product.Should().NotBeNull();
        product!.Name.Should().Be("Updated Product Name");
        product.Price.Should().Be(150.0m);
    }

    [Fact]
    public async Task DeleteProduct_WhenExists_ReturnsNoContent()
    {
        // Arrange - Create provider, category and product
        var providerResponse = await AuthenticatedPostAsync("/api/admin/providers", new
        {
            Name = "Delete Product Provider",
            WebsiteUrl = "https://deleteproduct.com"
        });
        var provider = await providerResponse.Content.ReadFromJsonAsync<AdminProviderDto>();

        var categoryResponse = await AuthenticatedPostAsync("/api/admin/categories", new
        {
            Name = "Delete Product Category",
            CocoClassId = 50007
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryDto>();

        var createResponse = await AuthenticatedPostAsync("/api/admin/products", new
        {
            Name = "Product To Delete",
            ProviderId = provider!.Id,
            CategoryId = category!.Id,
            Price = 25.0m
        });
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        created.Should().NotBeNull();

        // Act
        var response = await AuthenticatedDeleteAsync($"/api/admin/products/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await AuthenticatedGetAsync($"/api/admin/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Stats & System Tests

    [Fact]
    public async Task GetStats_ReturnsStatistics()
    {
        // Act
        var response = await AuthenticatedGetAsync("/api/admin/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<AdminDashboardStatsDto>();
        stats.Should().NotBeNull();
        stats!.Products.Should().BeGreaterOrEqualTo(0);
        stats.Providers.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetSystemStatus_ReturnsModelStatus()
    {
        // Act
        var response = await AuthenticatedGetAsync("/api/admin/system-status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<AdminSystemStatusDto>();
        status.Should().NotBeNull();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task AdminEndpoints_WithoutAuth_ReturnUnauthorized()
    {
        // Act
        var response = await Client!.GetAsync("/api/admin/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Response DTOs

    // Response DTOs are imported from VisualSearch.Api.Contracts.DTOs to stay aligned with API responses.

    private sealed record IdResponse(int Id);

    #endregion
}
