using BusinessObjects.Models;
using BusinessObjects.Results;
using DataAccessObjects;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class NewsArticleRepository(NewsArticleDAO newsArticleDAO) : INewsArticleRepository
{
    public Task<IReadOnlyList<NewsArticle>> GetActiveAsync(CancellationToken cancellationToken = default)
        => newsArticleDAO.GetActiveAsync(cancellationToken);

    public Task<IReadOnlyList<NewsArticle>> SearchAsync(string? search, bool? status, short? categoryId, CancellationToken cancellationToken = default)
        => newsArticleDAO.SearchAsync(search, status, categoryId, cancellationToken);
    public Task<NewsArticle?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => newsArticleDAO.GetByIdAsync(id, cancellationToken);
    public Task<IReadOnlyList<NewsArticle>> GetByAuthorAsync(short authorId, CancellationToken cancellationToken = default)
        => newsArticleDAO.GetByAuthorAsync(authorId, cancellationToken);
    public Task<IReadOnlyList<NewsArticle>> GetCreatedInRangeAsync(DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
        => newsArticleDAO.GetCreatedInRangeAsync(startDate, endDate, cancellationToken);
    public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default)
        => newsArticleDAO.GetTagsAsync(cancellationToken);
    public Task<NewsWriteStatus> CreateAsync(NewsArticle article, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default)
        => newsArticleDAO.CreateAsync(article, tagIds, cancellationToken);
    public Task<NewsWriteStatus> UpdateAsync(NewsArticle article, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default)
        => newsArticleDAO.UpdateAsync(article, tagIds, cancellationToken);
    public Task<NewsWriteStatus> DeleteAsync(string id, CancellationToken cancellationToken = default)
        => newsArticleDAO.DeleteAsync(id, cancellationToken);
}
