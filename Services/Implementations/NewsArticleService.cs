using BusinessObjects.Models;
using BusinessObjects.Results;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Models;

namespace Services.Implementations;

public sealed class NewsArticleService(INewsArticleRepository repository) : INewsArticleService
{
    public Task<IReadOnlyList<NewsArticle>> GetActiveAsync(CancellationToken cancellationToken = default)
        => repository.GetActiveAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsArticleDetails>> SearchAsync(string? search, bool? status, short? categoryId, CancellationToken cancellationToken = default)
        => (await repository.SearchAsync(search?.Trim(), status, categoryId, cancellationToken)).Select(ToDetails).ToList();

    public async Task<NewsArticleDetails?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var article = await repository.GetByIdAsync(id, cancellationToken);
        return article is null ? null : ToDetails(article);
    }

    public async Task<IReadOnlyList<NewsArticleDetails>> GetByAuthorAsync(short authorId, CancellationToken cancellationToken = default)
        => (await repository.GetByAuthorAsync(authorId, cancellationToken)).Select(ToDetails).ToList();

    public async Task<NewsReport> GetReportAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        startDate = startDate.Date;
        endDate = endDate.Date;
        if (startDate > endDate)
            throw new ArgumentException("StartDate must not be after EndDate.");

        var articles = await repository.GetCreatedInRangeAsync(startDate, endDate, cancellationToken);
        return new NewsReport(startDate, endDate, articles.Select(article => new NewsReportRow(
            article.NewsArticleId, article.NewsTitle ?? article.Headline, article.Headline,
            article.CreatedDate!.Value, article.Category?.CategoryName, article.CreatedBy?.AccountName,
            article.NewsStatus)).ToList());
    }

    public async Task<IReadOnlyList<TagOption>> GetTagOptionsAsync(CancellationToken cancellationToken = default)
        => (await repository.GetTagsAsync(cancellationToken)).Select(ToTagOption).ToList();

    public async Task<NewsOperationResult> CreateAsync(NewsArticleInput input, short staffId, CancellationToken cancellationToken = default)
    {
        var error = Validate(input);
        if (error is not null) return error;
        var article = ToEntity(input);
        article.NewsArticleId = Guid.NewGuid().ToString("N")[..20];
        article.CreatedById = staffId;
        article.CreatedDate = DateTime.Now;
        return ToResult(await repository.CreateAsync(article, input.TagIds.Distinct().ToArray(), cancellationToken));
    }

    public async Task<NewsOperationResult> UpdateAsync(string id, NewsArticleInput input, short staffId, CancellationToken cancellationToken = default)
    {
        var error = Validate(input);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 20)
            return NewsOperationResult.Failure("Invalid article ID.");
        var article = ToEntity(input);
        article.NewsArticleId = id;
        article.UpdatedById = staffId;
        article.ModifiedDate = DateTime.Now;
        return ToResult(await repository.UpdateAsync(article, input.TagIds.Distinct().ToArray(), cancellationToken));
    }

    public async Task<NewsOperationResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
        => ToResult(await repository.DeleteAsync(id, cancellationToken));

    private static NewsOperationResult? Validate(NewsArticleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Headline) || input.Headline.Trim().Length > 150)
            return NewsOperationResult.Failure("Headline is required and must be at most 150 characters.", "Headline");
        if (input.Title?.Length > 400)
            return NewsOperationResult.Failure("Title must be at most 400 characters.", "Title");
        if (input.Content?.Length > 4000)
            return NewsOperationResult.Failure("Content must be at most 4000 characters.", "Content");
        if (input.Source?.Length > 400)
            return NewsOperationResult.Failure("Source must be at most 400 characters.", "Source");
        if (!input.Status.HasValue)
            return NewsOperationResult.Failure("Choose Active or Inactive.", "Status");
        return null;
    }

    private static NewsArticle ToEntity(NewsArticleInput input) => new()
    {
        NewsTitle = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim(),
        Headline = input.Headline.Trim(), NewsContent = input.Content, NewsSource = input.Source?.Trim(),
        CategoryId = input.CategoryId, NewsStatus = input.Status
    };

    private static TagOption ToTagOption(Tag tag) => new(tag.TagId, tag.TagName ?? $"Tag #{tag.TagId}", tag.Note);
    private static NewsArticleDetails ToDetails(NewsArticle article)
        => new(article.NewsArticleId, article.NewsTitle, article.Headline, article.NewsContent, article.NewsSource,
            article.CategoryId, article.Category?.CategoryName, article.NewsStatus, article.CreatedById,
            article.CreatedBy?.AccountName, article.CreatedDate, article.UpdatedById, article.ModifiedDate,
            article.Tags.OrderBy(tag => tag.TagName).ThenBy(tag => tag.TagId).Select(ToTagOption).ToList());

    private static NewsOperationResult ToResult(NewsWriteStatus status) => status switch
    {
        NewsWriteStatus.Success => NewsOperationResult.Success(),
        NewsWriteStatus.NotFound => NewsOperationResult.Failure("This article no longer exists. Refresh the list and try again."),
        NewsWriteStatus.CategoryNotFound => NewsOperationResult.Failure("The selected category no longer exists.", "CategoryId"),
        NewsWriteStatus.TagNotFound => NewsOperationResult.Failure("One or more selected tags no longer exist. Choose tags again.", "TagIds"),
        NewsWriteStatus.InvalidAuthor => NewsOperationResult.Failure("Your Staff account is no longer available. Please sign in again."),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
