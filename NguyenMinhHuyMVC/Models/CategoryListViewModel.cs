using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class CategoryListViewModel
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyList<CategorySummary> Categories { get; set; } = [];
}
