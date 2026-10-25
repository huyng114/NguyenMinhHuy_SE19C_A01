using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Services.Models;

namespace NguyenMinhHuyMVC.Models;

public sealed class NewsReportViewModel
{
    private static DateTime FirstDayOfCurrentMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [Required(ErrorMessage = "Choose a start date."), DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime? StartDate { get; set; } = FirstDayOfCurrentMonth;

    [Required(ErrorMessage = "Choose an end date."), DataType(DataType.Date), Display(Name = "End date")]
    public DateTime? EndDate { get; set; } = DateTime.Today;

    [BindNever, ValidateNever]
    public NewsReport? Report { get; set; }
}
