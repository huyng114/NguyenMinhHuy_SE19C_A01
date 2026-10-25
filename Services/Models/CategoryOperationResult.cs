namespace Services.Models;

public sealed record CategoryOperationResult(bool Succeeded, string? Error = null, string Field = "")
{
    public static CategoryOperationResult Success() => new(true);
    public static CategoryOperationResult Failure(string error, string field = "") => new(false, error, field);
}
