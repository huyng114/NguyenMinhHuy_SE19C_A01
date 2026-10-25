using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class NewsListViewModel
{
    public string? Search { get; set; }
    public bool? Status { get; set; }
    public short? CategoryId { get; set; }
    public IReadOnlyList<NewsArticleDetails> Articles { get; set; } = [];
    public IReadOnlyList<CategorySummary> Categories { get; set; } = [];
}
