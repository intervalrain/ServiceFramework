namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

/// <summary>
/// Auto-convention decision result
/// </summary>
public class ConventionDecisionResult
{
    public ConventionMode Mode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsError => !string.IsNullOrEmpty(ErrorMessage);

    public static ConventionDecisionResult Success(ConventionMode mode)
        => new() { Mode = mode };

    public static ConventionDecisionResult Error(string errorMessage)
        => new() { ErrorMessage = errorMessage };
}

/// <summary>
/// Convention mode types based on decision tree
/// </summary>
public enum ConventionMode
{
    RequestResponse,
    PubSubPushJetStream,
    PubSubPullJetStream,
    PubSubPushClassic,
}