using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for category-related business operations.
/// </summary>
public sealed class CategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
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
