using System.Reflection;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

public class AutoConventionSetting
{
    public Assembly Assembly { get; set; } = null!;
    public Func<Type, bool>? TypePredicate { get; set; }
    public string? RoutePrefix { get; set; }

    public override string ToString() => Assembly.ToString();
}