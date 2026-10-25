namespace Services.Models;

public sealed record AccountOperationResult(bool Succeeded, string? Error = null, string Field = "")
{
    public static AccountOperationResult Success() => new(true);
    public static AccountOperationResult Failure(string error, string field = "") => new(false, error, field);
}
