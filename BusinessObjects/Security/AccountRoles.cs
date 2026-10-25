namespace BusinessObjects.Security;

public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Lecturer = "Lecturer";

    public static string? FromDatabase(int? role) => role switch
    {
        1 => Staff,
        2 => Lecturer,
        _ => null
    };
}
