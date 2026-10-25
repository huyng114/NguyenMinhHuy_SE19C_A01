using System.Data;
using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects.Context;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects;

public sealed class CategoryDAO(FUNewsManagementContext context)
{
    public async Task<IReadOnlyList<Category>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking().Include(category => category.ParentCategory).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(category => category.CategoryName.Contains(search) || category.CategoryDesciption.Contains(search));
        if (isActive.HasValue)
            query = query.Where(category => category.IsActive == isActive.Value);
        return await query.OrderBy(category => category.CategoryName).ThenBy(category => category.CategoryId).ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(short id, CancellationToken cancellationToken = default)
        => context.Categories.AsNoTracking().Include(category => category.ParentCategory)
            .FirstOrDefaultAsync(category => category.CategoryId == id, cancellationToken);

    public async Task<CategoryWriteStatus> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var status = await ValidateParentAsync(null, category.ParentCategoryId, cancellationToken);
        if (status != CategoryWriteStatus.Success)
            return status;

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CategoryWriteStatus.Success;
    }

    public async Task<CategoryWriteStatus> UpdateAsync(Category changes, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var category = await context.Categories.FindAsync([changes.CategoryId], cancellationToken);
        if (category is null)
            return CategoryWriteStatus.NotFound;
        var status = await ValidateParentAsync(changes.CategoryId, changes.ParentCategoryId, cancellationToken);
        if (status != CategoryWriteStatus.Success)
            return status;

        category.CategoryName = changes.CategoryName;
        category.CategoryDesciption = changes.CategoryDesciption;
        category.ParentCategoryId = changes.ParentCategoryId;
        category.IsActive = changes.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CategoryWriteStatus.Success;
    }

    public async Task<CategoryWriteStatus> DeleteAsync(short id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var category = await context.Categories.FindAsync([id], cancellationToken);
        if (category is null)
            return CategoryWriteStatus.NotFound;
        // Check every news status before deletion to prevent the database cascade from deleting articles.
        if (await context.NewsArticles.AnyAsync(article => article.CategoryId == id, cancellationToken))
            return CategoryWriteStatus.InUse;
        if (await context.Categories.AnyAsync(child => child.ParentCategoryId == id && child.CategoryId != id, cancellationToken))
            return CategoryWriteStatus.HasChildren;

        context.Categories.Remove(category);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CategoryWriteStatus.Success;
    }

    private async Task<CategoryWriteStatus> ValidateParentAsync(short? id, short? parentId, CancellationToken cancellationToken)
    {
        // Validate and hold the ancestor reads within the write transaction so concurrent edits cannot create a cycle.
        var visited = new HashSet<short>();
        while (parentId.HasValue)
        {
            if (parentId == id || !visited.Add(parentId.Value))
                return CategoryWriteStatus.InvalidHierarchy;
            var parent = await context.Categories.AsNoTracking()
                .Where(category => category.CategoryId == parentId.Value)
                .Select(category => new { category.ParentCategoryId }).FirstOrDefaultAsync(cancellationToken);
            if (parent is null)
                return CategoryWriteStatus.ParentNotFound;
            parentId = parent.ParentCategoryId;
        }
        return CategoryWriteStatus.Success;
    }
}
