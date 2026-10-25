namespace Services.Models;

public sealed record NewsArticleInput(string? Title, string Headline, string? Content, string? Source,
    short? CategoryId, bool? Status, IReadOnlyCollection<int> TagIds);
