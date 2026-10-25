namespace Services.Models;

// Only safe identity information leaves the authentication service.
public sealed record AuthenticatedAccount(string Id, string Name, string Email, string Role);
