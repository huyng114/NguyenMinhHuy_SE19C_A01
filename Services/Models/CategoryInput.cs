namespace Services.Models;

public sealed record CategoryInput(string Name, string Description, short? ParentId, bool? IsActive);
