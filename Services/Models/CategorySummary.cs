namespace Services.Models;

public sealed record CategorySummary(short Id, string Name, string Description, short? ParentId, string? ParentName, bool? IsActive);
