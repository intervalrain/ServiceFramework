using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;

/// <summary>
/// Interface for making auto-convention decisions
/// </summary>
public interface IConventionDecisionMaker
{
    /// <summary>
    /// Make decision based on method information and options
    /// </summary>
    /// <param name="context">Decision context</param>
    /// <returns>Decision result</returns>
    ConventionDecisionResult MakeDecision(ConventionDecisionContext context);
}