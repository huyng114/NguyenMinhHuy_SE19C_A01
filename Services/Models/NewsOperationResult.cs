namespace Services.Models;

public sealed record NewsOperationResult(bool Succeeded, string? Error = null, string Field = "")
{
    public static NewsOperationResult Success() => new(true);
    public static NewsOperationResult Failure(string error, string field = "") => new(false, error, field);
}
