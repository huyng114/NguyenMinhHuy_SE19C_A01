namespace Services.Models;

public sealed record TagOption(int Id, string Name, string? Note);

public sealed record NewsArticleDetails(string Id, string? Title, string Headline, string? Content, string? Source,
    short? CategoryId, string? CategoryName, bool? Status, short? CreatedById, string? CreatedByName,
    DateTime? CreatedDate, short? UpdatedById, DateTime? ModifiedDate, IReadOnlyList<TagOption> Tags);
