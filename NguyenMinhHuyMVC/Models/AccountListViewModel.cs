using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class AccountListViewModel
{
    public string? Search { get; set; }
    public int? Role { get; set; }
    public IReadOnlyList<AccountSummary> Accounts { get; set; } = [];
}
