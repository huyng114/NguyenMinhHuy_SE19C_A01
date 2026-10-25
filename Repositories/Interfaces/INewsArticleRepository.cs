using BusinessObjects.Models;
using BusinessObjects.Results;

namespace Repositories.Interfaces;

public interface INewsArticleRepository
{
    Task<IReadOnlyList<NewsArticle>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsArticle>> SearchAsync(string? search, bool? status, short? categoryId, CancellationToken cancellationToken = default);
    Task<NewsArticle?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsArticle>> GetByAuthorAsync(short authorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsArticle>> GetCreatedInRangeAsync(DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default);
    Task<NewsWriteStatus> CreateAsync(NewsArticle article, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default);
    Task<NewsWriteStatus> UpdateAsync(NewsArticle article, IReadOnlyCollection<int> tagIds, CancellationToken cancellationToken = default);
    Task<NewsWriteStatus> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
