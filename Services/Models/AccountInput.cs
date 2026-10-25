namespace Services.Models;

public sealed record AccountInput(string Name, string Email, int Role, string? Password);
