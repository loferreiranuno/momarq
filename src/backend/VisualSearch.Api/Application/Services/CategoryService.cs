using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for category-related business operations.
/// </summary>
public sealed class CategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ObjectDetectionService _detectionService;

    public CategoryService(ICategoryRepository categoryRepository, ObjectDetectionService detectionService)
    {
        _categoryRepository = categoryRepository;
        _detectionService = detectionService;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(MapToDto);
    }

    /// <summary>
    /// Gets categories with optional detection enabled filter.
    /// </summary>
    public async Task<IEnumerable<CategorySummaryDto>> GetCategoriesAsync(bool? detectionEnabled = null, CancellationToken cancellationToken = default)
    {
        var categories = detectionEnabled.HasValue
            ? await _categoryRepository.GetByDetectionEnabledAsync(detectionEnabled.Value, cancellationToken)
            : await _categoryRepository.GetAllAsync(cancellationToken);

        return categories
            .OrderBy(c => c.Name)
            .Select(c => new CategorySummaryDto(c.Id, c.Name, c.CocoClassId, c.DetectionEnabled));
    }

    /// <summary>
    /// Gets all categories with product counts for admin dashboard.
    /// </summary>
    public async Task<IEnumerable<AdminCategoryDto>> GetAllAdminCategoriesAsync(bool? detectionEnabled = null, CancellationToken cancellationToken = default)
    {
        var categoriesWithCounts = await _categoryRepository.GetAllWithProductCountsAsync(cancellationToken);

        var result = categoriesWithCounts.AsEnumerable();
        if (detectionEnabled.HasValue)
        {
            result = result.Where(c => c.Category.DetectionEnabled == detectionEnabled.Value);
        }

        return result
            .OrderBy(c => c.Category.Name)
            .Select(c => new AdminCategoryDto
            {
                Id = c.Category.Id,
                Name = c.Category.Name,
                CocoClassId = c.Category.CocoClassId,
                DetectionEnabled = c.Category.DetectionEnabled,
                ProductCount = c.ProductCount,
                CreatedAt = c.Category.CreatedAt
            });
    }

    public async Task<IEnumerable<CategorySummaryDto>> GetEnabledCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetEnabledForDetectionAsync(cancellationToken);
        return categories.Select(c => new CategorySummaryDto(c.Id, c.Name, c.CocoClassId, c.DetectionEnabled));
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : MapToDto(category);
    }

    /// <summary>
    /// Gets a category by ID with product count for admin dashboard.
    /// </summary>
    public async Task<AdminCategoryDto?> GetAdminCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var (category, productCount) = await _categoryRepository.GetByIdWithProductCountAsync(id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        return new AdminCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CocoClassId = category.CocoClassId,
            DetectionEnabled = category.DetectionEnabled,
            ProductCount = productCount,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        // Check if category with same name exists
        var existing = await _categoryRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Category with name '{request.Name}' already exists.");
        }

        // Check if category with same COCO class ID exists
        var existingCoco = await _categoryRepository.GetByCocoClassIdAsync(request.CocoClassId, cancellationToken);
        if (existingCoco is not null)
        {
            throw new InvalidOperationException($"Category with COCO class ID '{request.CocoClassId}' already exists.");
        }

        var category = new Category
        {
            Name = request.Name,
            CocoClassId = request.CocoClassId,
            DetectionEnabled = request.DetectionEnabled,
            CreatedAt = DateTime.UtcNow
        };

        await _categoryRepository.AddAsync(category, cancellationToken);
        return MapToDto(category);
    }

    /// <summary>
    /// Creates a category and returns admin DTO with refreshed detection service.
    /// </summary>
    public async Task<AdminCategoryDto> CreateAdminCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Category name is required.", nameof(request));
        }

        // Check for duplicate name
        var existingName = await _categoryRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingName is not null)
        {
            throw new InvalidOperationException($"Category with name '{request.Name}' already exists.");
        }

        // Check for duplicate COCO class ID
        var existingCoco = await _categoryRepository.GetByCocoClassIdAsync(request.CocoClassId, cancellationToken);
        if (existingCoco is not null)
        {
            throw new InvalidOperationException($"Category with COCO class ID {request.CocoClassId} already exists.");
        }

        var category = new Category
        {
            Name = request.Name,
            CocoClassId = request.CocoClassId,
            DetectionEnabled = request.DetectionEnabled,
            CreatedAt = DateTime.UtcNow
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        // Refresh detection service cache
        await _detectionService.RefreshEnabledCategoriesAsync(cancellationToken);

        return new AdminCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CocoClassId = category.CocoClassId,
            DetectionEnabled = category.DetectionEnabled,
            ProductCount = 0,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        // Check if another category with same name exists
        var existingName = await _categoryRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingName is not null && existingName.Id != id)
        {
            throw new InvalidOperationException($"Category with name '{request.Name}' already exists.");
        }

        // Check if another category with same COCO class ID exists
        var existingCoco = await _categoryRepository.GetByCocoClassIdAsync(request.CocoClassId, cancellationToken);
        if (existingCoco is not null && existingCoco.Id != id)
        {
            throw new InvalidOperationException($"Category with COCO class ID '{request.CocoClassId}' already exists.");
        }

        category.Name = request.Name;
        category.CocoClassId = request.CocoClassId;
        category.DetectionEnabled = request.DetectionEnabled;

        await _categoryRepository.UpdateAsync(category, cancellationToken);
        return MapToDto(category);
    }

    /// <summary>
    /// Updates a category with partial data and returns admin DTO with refreshed detection service.
    /// </summary>
    public async Task<AdminCategoryDto?> UpdateAdminCategoryAsync(
        int id,
        string? name,
        int? cocoClassId,
        bool? detectionEnabled,
        CancellationToken cancellationToken = default)
    {
        var (category, productCount) = await _categoryRepository.GetByIdWithProductCountAsync(id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name) && name != category.Name)
        {
            var existingName = await _categoryRepository.GetByNameAsync(name, cancellationToken);
            if (existingName is not null && existingName.Id != id)
            {
                throw new InvalidOperationException($"Category with name '{name}' already exists.");
            }
            category.Name = name;
        }

        if (cocoClassId.HasValue && cocoClassId.Value != category.CocoClassId)
        {
            var existingCoco = await _categoryRepository.GetByCocoClassIdAsync(cocoClassId.Value, cancellationToken);
            if (existingCoco is not null && existingCoco.Id != id)
            {
                throw new InvalidOperationException($"Category with COCO class ID {cocoClassId.Value} already exists.");
            }
            category.CocoClassId = cocoClassId.Value;
        }

        if (detectionEnabled.HasValue)
        {
            category.DetectionEnabled = detectionEnabled.Value;
        }

        await _categoryRepository.UpdateAsync(category, cancellationToken);

        // Refresh detection service cache
        await _detectionService.RefreshEnabledCategoriesAsync(cancellationToken);

        return new AdminCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CocoClassId = category.CocoClassId,
            DetectionEnabled = category.DetectionEnabled,
            ProductCount = productCount,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return false;
        }

        await _categoryRepository.DeleteAsync(category, cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes a category. Throws if products are associated.
    /// </summary>
    public async Task<bool> DeleteAdminCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetWithProductsAsync(id, cancellationToken);
        if (category is null)
        {
            return false;
        }

        if (category.Products.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete category '{category.Name}' because it has {category.Products.Count} associated product(s). " +
                "Please reassign or delete those products first.");
        }

        await _categoryRepository.DeleteAsync(category, cancellationToken);
        return true;
    }

    public async Task<CategoryDto?> ToggleDetectionAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        await _categoryRepository.ToggleDetectionAsync(id, enabled, cancellationToken);
        
        // Refresh the entity
        category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : MapToDto(category);
    }

    /// <summary>
    /// Toggles detection and returns admin DTO with refreshed detection service.
    /// </summary>
    public async Task<AdminCategoryDto?> ToggleAdminDetectionAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var (category, productCount) = await _categoryRepository.GetByIdWithProductCountAsync(id, cancellationToken);
        if (category is null)
        {
            return null;
        }

        category.DetectionEnabled = enabled;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        // Refresh detection service cache
        await _detectionService.RefreshEnabledCategoriesAsync(cancellationToken);

        return new AdminCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CocoClassId = category.CocoClassId,
            DetectionEnabled = category.DetectionEnabled,
            ProductCount = productCount,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<IEnumerable<int>> GetEnabledCocoClassIdsAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetEnabledForDetectionAsync(cancellationToken);
        return categories.Select(c => c.CocoClassId);
    }

    private static CategoryDto MapToDto(Category category)
    {
        return new CategoryDto(
            category.Id,
            category.Name,
            null, // Description field not in current entity
            category.CocoClassId,
            category.DetectionEnabled,
            category.CreatedAt,
            null // UpdatedAt field not in current entity
        );
    }
}
