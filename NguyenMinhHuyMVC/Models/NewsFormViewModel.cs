using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class NewsFormViewModel
{
    [StringLength(20)]
    public string? Id { get; set; }
    [StringLength(400)]
    public string? Title { get; set; }
    [Required, StringLength(150)]
    public string Headline { get; set; } = string.Empty;
    [StringLength(4000)]
    public string? Content { get; set; }
    [StringLength(400)]
    public string? Source { get; set; }
    [Display(Name = "Category")]
    public short? CategoryId { get; set; }
    [Required(ErrorMessage = "Choose Active or Inactive.")]
    public bool? Status { get; set; } = false;
    [Display(Name = "Tags")]
    public List<int> TagIds { get; set; } = [];
    [BindNever, ValidateNever]
    public IReadOnlyList<CategorySummary> Categories { get; set; } = [];
    [BindNever, ValidateNever]
    public IReadOnlyList<TagOption> Tags { get; set; } = [];
}
