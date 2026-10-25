using BusinessObjects.Models;
using Services.Models;

namespace Services.Interfaces;

public interface INewsArticleService
{
    Task<IReadOnlyList<NewsArticle>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsArticleDetails>> SearchAsync(string? search, bool? status, short? categoryId, CancellationToken cancellationToken = default);
    Task<NewsArticleDetails?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsArticleDetails>> GetByAuthorAsync(short authorId, CancellationToken cancellationToken = default);
    Task<NewsReport> GetReportAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagOption>> GetTagOptionsAsync(CancellationToken cancellationToken = default);
    Task<NewsOperationResult> CreateAsync(NewsArticleInput input, short staffId, CancellationToken cancellationToken = default);
    Task<NewsOperationResult> UpdateAsync(string id, NewsArticleInput input, short staffId, CancellationToken cancellationToken = default);
    Task<NewsOperationResult> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
