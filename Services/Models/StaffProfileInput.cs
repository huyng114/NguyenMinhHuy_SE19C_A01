namespace Services.Models;

public sealed record StaffProfileInput(string Name, string Email, string CurrentPassword, string? NewPassword);
