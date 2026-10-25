using BusinessObjects.Models;
using BusinessObjects.Results;

namespace Repositories.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(short id, CancellationToken cancellationToken = default);
    Task<CategoryWriteStatus> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<CategoryWriteStatus> UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task<CategoryWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default);
}
