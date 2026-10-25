using System.ComponentModel.DataAnnotations;

namespace NguyenMinhHuyMVC.Models;

public sealed class StaffProfileViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(70)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(70), DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [StringLength(70), DataType(DataType.Password), Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "New passwords do not match."),
     DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string? ConfirmNewPassword { get; set; }
}
