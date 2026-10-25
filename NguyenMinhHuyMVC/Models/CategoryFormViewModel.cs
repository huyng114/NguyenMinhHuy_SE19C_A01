using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class CategoryFormViewModel
{
    public short? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Parent category")]
    public short? ParentId { get; set; }

    [Required(ErrorMessage = "Choose Active or Inactive."), Display(Name = "Status")]
    public bool? IsActive { get; set; } = true;

    [BindNever, ValidateNever]
    public IReadOnlyList<CategorySummary> ParentOptions { get; set; } = [];
}
