using System.ComponentModel.DataAnnotations;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

public class AutoConventionOptions
{
    public const string Section = "AutoConvention";


    [MaxLength(16, ErrorMessage = "RoutePrefix is at most 16 characters long")]
    public string? RoutePrefix { get; set; } = "nats";

    public List<AutoConventionSetting>? Settings { get; set; }

    public bool UseExceptionHandler { get; set; } = true;

    public bool DefaultJetStreamEnable { get; set; } = true;
}