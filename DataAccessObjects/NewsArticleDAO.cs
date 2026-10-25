using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DataAccessObjects;

public sealed class NewsArticleDAO(FUNewsManagementContext context)
{
    public async Task<IReadOnlyList<NewsArticle>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await context.NewsArticles.AsNoTracking().Include(article => article.Tags).Include(article => article.Category)
            .Where(article => article.NewsStatus == true)
            .OrderByDescending(article => article.CreatedDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(string? search, bool? status, short? categoryId, CancellationToken cancellationToken = default)
    {
        var query = context.NewsArticles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(article => (article.NewsTitle != null && article.NewsTitle.Contains(search)) ||
                article.Headline.Contains(search) || (article.NewsContent != null && article.NewsContent.Contains(search)) ||
                article.Tags.Any(tag => tag.TagName != null && tag.TagName.Contains(search)));
        if (status.HasValue)
            query = query.Where(article => article.NewsStatus == status);
        if (categoryId.HasValue)
            query = query.Where(article => article.CategoryId == categoryId);
        return await query.Include(article => article.Category).Include(article => article.CreatedBy).Include(article => article.Tags)
            .OrderByDescending(article => article.CreatedDate).ThenBy(article => article.NewsArticleId).ToListAsync(cancellationToken);
    }

    public Task<NewsArticle?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => context.NewsArticles.AsNoTracking().Include(article => article.Category)
            .Include(article => article.CreatedBy).Include(article => article.Tags)
            .FirstOrDefaultAsync(article => article.NewsArticleId == id, cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> GetByAuthorAsync(short authorId, CancellationToken cancellationToken = default)
        => await context.NewsArticles.AsNoTracking()
            .Where(article => article.CreatedById == authorId)
            .Include(article => article.Category).Include(article => article.Tags)
            .OrderByDescending(article => article.CreatedDate).ThenBy(article => article.NewsArticleId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticle>> GetCreatedInRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var query = context.NewsArticles.AsNoTracking()
            .Where(article => article.CreatedDate >= startDate.Date);
        // Use the next day's midnight as an exclusive bound so EndDate includes its whole day.
        // DateTime's maximum day has no next day and needs no upper bound.
        if (endDate.Date < DateTime.MaxValue.Date)
        {
            var endExclusive = endDate.Date.AddDays(1);
            query = query.Where(article => article.CreatedDate < endExclusive);
        }
        return await query.Include(article => article.Category).Include(article => article.CreatedBy)
            .OrderByDescending(article => article.CreatedDate).ThenBy(article => article.NewsArticleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default)
        => await context.Tags.AsNoTracking().OrderBy(tag => tag.TagName).ThenBy(tag => tag.TagId).ToListAsync(cancellationToken);

    public async Task<NewsWriteStatus> CreateAsync(NewsArticle article, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var status = await ValidateReferencesAsync(article.CategoryId, article.CreatedById, tagIds, cancellationToken);
        if (status != NewsWriteStatus.Success)
            return status;
        article.Tags = await context.Tags.Where(tag => tagIds.Contains(tag.TagId)).ToListAsync(cancellationToken);
        context.NewsArticles.Add(article);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NewsWriteStatus.Success;
    }

    public async Task<NewsWriteStatus> UpdateAsync(NewsArticle changes, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var article = await context.NewsArticles.Include(item => item.Tags)
            .FirstOrDefaultAsync(item => item.NewsArticleId == changes.NewsArticleId, cancellationToken);
        if (article is null)
            return NewsWriteStatus.NotFound;
        var status = await ValidateReferencesAsync(changes.CategoryId, changes.UpdatedById, tagIds, cancellationToken);
        if (status != NewsWriteStatus.Success)
            return status;

        article.NewsTitle = changes.NewsTitle;
        article.Headline = changes.Headline;
        article.NewsContent = changes.NewsContent;
        article.NewsSource = changes.NewsSource;
        article.CategoryId = changes.CategoryId;
        article.NewsStatus = changes.NewsStatus;
        article.UpdatedById = changes.UpdatedById;
        article.ModifiedDate = changes.ModifiedDate;
        // Preserve CreatedByID/CreatedDate and replace only the selected tag associations.
        var selectedTags = await context.Tags.Where(tag => tagIds.Contains(tag.TagId)).ToListAsync(cancellationToken);
        foreach (var oldTag in article.Tags.Where(tag => !tagIds.Contains(tag.TagId)).ToList())
            article.Tags.Remove(oldTag);
        foreach (var tag in selectedTags.Where(tag => article.Tags.All(existing => existing.TagId != tag.TagId)))
            article.Tags.Add(tag);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NewsWriteStatus.Success;
    }

    public async Task<NewsWriteStatus> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var article = await context.NewsArticles.FindAsync([id], cancellationToken);
        if (article is null)
            return NewsWriteStatus.NotFound;
        // NewsTag has no cascade delete in the supplied schema. Remove links explicitly, preserving Tag rows.
        await context.Set<Dictionary<string, object>>("NewsTag")
            .Where(link => EF.Property<string>(link, "NewsArticleId") == id).ExecuteDeleteAsync(cancellationToken);
        context.NewsArticles.Remove(article);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NewsWriteStatus.Success;
    }

    private async Task<NewsWriteStatus> ValidateReferencesAsync(short? categoryId, short? authorId, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken)
    {
        if (!await context.SystemAccounts.AnyAsync(account => account.AccountId == authorId && account.AccountRole == 1, cancellationToken))
            return NewsWriteStatus.InvalidAuthor;
        if (categoryId.HasValue && !await context.Categories.AnyAsync(category => category.CategoryId == categoryId, cancellationToken))
            return NewsWriteStatus.CategoryNotFound;
        if (await context.Tags.CountAsync(tag => tagIds.Contains(tag.TagId), cancellationToken) != tagIds.Count)
            return NewsWriteStatus.TagNotFound;
        return NewsWriteStatus.Success;
    }
}
