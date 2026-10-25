using Services.Models;

namespace Services.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategorySummary>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<CategorySummary?> GetByIdAsync(short id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategorySummary>> GetParentOptionsAsync(short? editingId, CancellationToken cancellationToken = default);
    Task<CategoryOperationResult> CreateAsync(CategoryInput input, CancellationToken cancellationToken = default);
    Task<CategoryOperationResult> UpdateAsync(short id, CategoryInput input, CancellationToken cancellationToken = default);
    Task<CategoryOperationResult> DeleteAsync(short id, CancellationToken cancellationToken = default);
}
