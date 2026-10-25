namespace Services.Models;

public sealed record NewsReportRow(string Id, string Title, string Headline, DateTime CreatedDate,
    string? CategoryName, string? AuthorName, bool? Status);

public sealed record NewsReport(DateTime StartDate, DateTime EndDate, IReadOnlyList<NewsReportRow> Rows)
{
    public int Total => Rows.Count;
    public int Active => Rows.Count(row => row.Status == true);
    public int Inactive => Rows.Count(row => row.Status == false);
    public int Unspecified => Rows.Count(row => row.Status is null);
}
