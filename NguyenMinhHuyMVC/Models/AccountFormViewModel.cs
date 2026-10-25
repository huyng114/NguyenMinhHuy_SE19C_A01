using System.ComponentModel.DataAnnotations;

namespace NguyenMinhHuyMVC.Models;

public sealed class AccountFormViewModel
{
    public short? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(70)]
    public string Email { get; set; } = string.Empty;

    [Range(1, 2, ErrorMessage = "Choose Staff or Lecturer.")]
    public int Role { get; set; } = 1;

    [StringLength(70), DataType(DataType.Password)]
    public string? Password { get; set; }
}
