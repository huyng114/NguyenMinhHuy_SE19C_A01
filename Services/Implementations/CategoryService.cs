using BusinessObjects.Models;
using BusinessObjects.Results;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Models;

namespace Services.Implementations;

public sealed class CategoryService(ICategoryRepository repository) : ICategoryService
{
    public async Task<IReadOnlyList<CategorySummary>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
        => (await repository.SearchAsync(search?.Trim(), isActive, cancellationToken)).Select(ToSummary).ToList();

    public async Task<CategorySummary?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : ToSummary(category);
    }

    public async Task<IReadOnlyList<CategorySummary>> GetParentOptionsAsync(short? editingId, CancellationToken cancellationToken = default)
    {
        var categories = await repository.SearchAsync(null, null, cancellationToken);
        var parents = categories.ToDictionary(category => category.CategoryId, category => category.ParentCategoryId);
        // Exclude the edited category, descendants and existing broken/cyclic chains.
        return categories.Where(category => CanBeParent(category.CategoryId, editingId, parents)).Select(ToSummary).ToList();
    }

    public async Task<CategoryOperationResult> CreateAsync(CategoryInput input, CancellationToken cancellationToken = default)
    {
        var error = Validate(input);
        return error ?? ToResult(await repository.CreateAsync(ToEntity(input), cancellationToken));
    }

    public async Task<CategoryOperationResult> UpdateAsync(short id, CategoryInput input, CancellationToken cancellationToken = default)
    {
        var error = Validate(input);
        if (error is not null)
            return error;
        if (input.ParentId == id)
            return CategoryOperationResult.Failure("A category cannot be its own parent.", "ParentId");
        var category = ToEntity(input);
        category.CategoryId = id;
        return ToResult(await repository.UpdateAsync(category, cancellationToken));
    }

    public async Task<CategoryOperationResult> DeleteAsync(short id, CancellationToken cancellationToken = default)
        => ToResult(await repository.DeleteAsync(id, cancellationToken));

    private static CategoryOperationResult? Validate(CategoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            return CategoryOperationResult.Failure("Name is required and must be at most 100 characters.", "Name");
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 250)
            return CategoryOperationResult.Failure("Description is required and must be at most 250 characters.", "Description");
        if (!input.IsActive.HasValue)
            return CategoryOperationResult.Failure("Choose Active or Inactive.", "IsActive");
        return null;
    }

    private static bool CanBeParent(short candidate, short? editingId, IReadOnlyDictionary<short, short?> parents)
    {
        var visited = new HashSet<short>();
        short? current = candidate;
        while (current.HasValue)
        {
            if (current == editingId || !visited.Add(current.Value) || !parents.TryGetValue(current.Value, out var parent))
                return false;
            current = parent;
        }
        return true;
    }

    private static Category ToEntity(CategoryInput input) => new()
    {
        CategoryName = input.Name.Trim(), CategoryDesciption = input.Description.Trim(),
        ParentCategoryId = input.ParentId, IsActive = input.IsActive
    };

    private static CategorySummary ToSummary(Category category)
        => new(category.CategoryId, category.CategoryName, category.CategoryDesciption,
            category.ParentCategoryId, category.ParentCategory?.CategoryName, category.IsActive);

    private static CategoryOperationResult ToResult(CategoryWriteStatus status) => status switch
    {
        CategoryWriteStatus.Success => CategoryOperationResult.Success(),
        CategoryWriteStatus.NotFound => CategoryOperationResult.Failure("This category no longer exists. Refresh the list and try again."),
        CategoryWriteStatus.ParentNotFound => CategoryOperationResult.Failure("The selected parent category no longer exists.", "ParentId"),
        CategoryWriteStatus.InvalidHierarchy => CategoryOperationResult.Failure("Choose a parent that does not create a circular category hierarchy.", "ParentId"),
        CategoryWriteStatus.InUse => CategoryOperationResult.Failure("This category cannot be deleted because it is used by news articles."),
        CategoryWriteStatus.HasChildren => CategoryOperationResult.Failure("Move or delete child categories before deleting this category."),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
