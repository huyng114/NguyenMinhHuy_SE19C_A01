using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class CategoryRepository(CategoryDAO categoryDAO) : ICategoryRepository
{
    public Task<IReadOnlyList<Category>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
        => categoryDAO.SearchAsync(search, isActive, cancellationToken);

    public Task<Category?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
        => categoryDAO.GetByIdAsync(id, cancellationToken);

    public Task<CategoryWriteStatus> CreateAsync(Category category, CancellationToken cancellationToken = default)
        => categoryDAO.CreateAsync(category, cancellationToken);

    public Task<CategoryWriteStatus> UpdateAsync(Category category, CancellationToken cancellationToken = default)
        => categoryDAO.UpdateAsync(category, cancellationToken);

    public Task<CategoryWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default)
        => categoryDAO.DeleteAsync(id, cancellationToken);
}
