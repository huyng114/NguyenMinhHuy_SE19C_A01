using System.ComponentModel.DataAnnotations;

namespace NguyenMinhHuyMVC.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(70)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(70)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
