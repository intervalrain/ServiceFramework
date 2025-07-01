using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;

/// <summary>
/// Convention decision maker following the decision tree logic
/// </summary>
public class ConventionDecisionMaker : IConventionDecisionMaker
{
    public ConventionDecisionResult MakeDecision(ConventionDecisionContext context)
    {
        // Check if JetStream is explicitly disabled
        bool jetStreamExplicitlyDisabled = context.JetStreamAttribute != null && !context.JetStreamAttribute.Enable;
        
        // (1) ReturnType 有無
        if (context.HasReturnValue)
        {
            // 有 ReturnType -> 檢查是否有 JetStreamPullAttribute
            if (context.HasJetStreamPullAttribute)
            {
                // ERROR1: Request/Response Mode 不可使用 JetStreamPullAttribute
                return ConventionDecisionResult.Error(
                    "Request/Response Mode cannot use JetStreamPullAttribute");
            }
            
            // (2) Request/Response Mode
            return ConventionDecisionResult.Success(ConventionMode.RequestResponse);
        }
        
        // 無 ReturnType -> (3) parameter 是否為 Collection
        if (context.IsParameterCollection)
        {
            // 是 Collection -> (4) 是否有使用 JetStreamPullAttribute
            if (context.HasJetStreamPullAttribute)
            {
                // 檢查是否有 JetStreamSubject
                if (context.HasJetStreamSubject)
                {
                    // ERROR2: Collection + JetStreamPullAttribute 不可同時使用 JetStreamSubject
                    return ConventionDecisionResult.Error(
                        "Collection + JetStreamPullAttribute cannot use JetStreamSubject simultaneously");
                }
                
                // (14) Pub/Sub Pull Mode with JetStream
                return ConventionDecisionResult.Success(ConventionMode.PubSubPullJetStream);
            }
            
            // 否 -> (6) 是否有使用 JetStreamSubject
            if (context.HasJetStreamSubject)
            {
                // (8) Pub/Sub Push Mode with JetStream
                return ConventionDecisionResult.Success(ConventionMode.PubSubPushJetStream);
            }
            
            // 檢查是否明確禁用 JetStream
            if (jetStreamExplicitlyDisabled)
            {
                // (13) Pub/Sub Push Mode classic
                return ConventionDecisionResult.Success(ConventionMode.PubSubPushClassic);
            }
            
            // (9) 看 AutoConventionOption.DefaultStreamEnabled
            if (context.Options.DefaultJetStreamEnable)
            {
                // (12) Pub/Sub Push Mode with JetStream
                return ConventionDecisionResult.Success(ConventionMode.PubSubPushJetStream);
            }
            
            // (13) Pub/Sub Push Mode classic
            return ConventionDecisionResult.Success(ConventionMode.PubSubPushClassic);
        }
        
        // 否 Collection -> (5) 是否有使用 JetStreamPullAttribute
        if (context.HasJetStreamPullAttribute)
        {
            // 檢查是否有 JetStreamSubject
            if (context.HasJetStreamSubject)
            {
                // ERROR3: Non-Collection + JetStreamPullAttribute 不可同時使用 JetStreamSubject
                return ConventionDecisionResult.Error(
                    "Non-Collection + JetStreamPullAttribute cannot use JetStreamSubject simultaneously");
            }
            
            // (15) Pub/Sub Pull Mode with JetStream
            return ConventionDecisionResult.Success(ConventionMode.PubSubPullJetStream);
        }
        
        // (7) 是否有使用 JetStreamSubject
        if (context.HasJetStreamSubject)
        {
            // (10) Pub/Sub Pull Mode with JetStream
            return ConventionDecisionResult.Success(ConventionMode.PubSubPullJetStream);
        }
        
        // 檢查是否明確禁用 JetStream
        if (jetStreamExplicitlyDisabled)
        {
            // ERROR4: Pull Mode 需要 JetStream，無法在明確禁用 JetStream 的情況下使用
            return ConventionDecisionResult.Error(
                "Pull Mode requires JetStream, cannot be used when JetStream is explicitly disabled");
        }
        
        // (11) 看 AutoConventionOption.DefaultStreamEnabled
        if (context.Options.DefaultJetStreamEnable)
        {
            // (16) Pub/Sub Pull Mode with JetStream
            return ConventionDecisionResult.Success(ConventionMode.PubSubPullJetStream);
        }
        
        // ERROR4: Pull Mode 需要 JetStream，無法在 non-js 環境下使用
        return ConventionDecisionResult.Error(
            "Pull Mode requires JetStream, cannot be used in non-js environment");
    }
}