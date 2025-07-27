using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using NATS.Client.Core;
using NSubstitute;
using System.Reflection;

namespace EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;

/// <summary>
/// Test fixtures and factories for creating test objects
/// </summary>
public static class TestFixtures
{
    /// <summary>
    /// Creates a test ActionExecutingContext for integration testing
    /// </summary>
    public static ActionExecutingContext CreateActionExecutingContext(
        string serviceName = "TestService",
        string methodName = "ProcessRequestAsync", 
        string subject = "test.request-response",
        ConventionMode conventionMode = ConventionMode.RequestResponse,
        object? actionArguments = null)
    {
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var actionDescriptor = new ActionDescriptor
        {
            DisplayName = $"{serviceName}.{methodName}",
            Properties = new Dictionary<object, object?>()
        };

        // Add metadata that the ActionContextMetadata constructor expects
        actionDescriptor.Properties["ServiceName"] = serviceName;
        actionDescriptor.Properties["MethodName"] = methodName;
        actionDescriptor.Properties["Subject"] = subject;
        actionDescriptor.Properties["ConventionMode"] = conventionMode;
        actionDescriptor.Properties["ChannelName"] = "default";

        // Add the original method if available
        var methodInfo = typeof(ITestNatsService).GetMethod(methodName);
        if (methodInfo != null)
        {
            actionDescriptor.Properties["OriginalMethod"] = methodInfo;
        }

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var filters = new List<IFilterMetadata>();

        var actionArgDict = new Dictionary<string, object?>();
        if (actionArguments != null)
        {
            actionArgDict["input"] = actionArguments;
        }

        return new ActionExecutingContext(actionContext, filters, actionArgDict, controller: null);
    }

    /// <summary>
    /// Creates a test NATS connection with common setup
    /// </summary>
    public static INatsConnection CreateTestNatsConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        // Simple test connection setup
        
        // Basic test connection setup
        // Actual NATS behavior is handled by the serializer adapters in the framework

        return connection;
    }

    /// <summary>
    /// Creates a test logger that can be verified
    /// </summary>
    public static ILogger<T> CreateTestLogger<T>()
    {
        return Substitute.For<ILogger<T>>();
    }

    /// <summary>
    /// Creates test audit wrapper request
    /// </summary>
    public static object CreateAuditWrapper(object? originalRequest = null)
    {
        return new
        {
            Data = originalRequest ?? "test data",
            ReqSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            UserId = "test-user",
            TenantId = "test-tenant",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Creates test response object
    /// </summary>
    public static object CreateTestResponse(object? data = null)
    {
        return new
        {
            Data = data ?? "test response",
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
    }
}

/// <summary>
/// Builder pattern for creating complex test scenarios
/// </summary>
public class TestScenarioBuilder
{
    private readonly Dictionary<string, object?> _properties = new();

    public TestScenarioBuilder WithServiceName(string serviceName)
    {
        _properties["ServiceName"] = serviceName;
        return this;
    }

    public TestScenarioBuilder WithMethodName(string methodName)
    {
        _properties["MethodName"] = methodName;
        return this;
    }

    public TestScenarioBuilder WithSubject(string subject)
    {
        _properties["Subject"] = subject;
        return this;
    }

    public TestScenarioBuilder WithConventionMode(ConventionMode mode)
    {
        _properties["ConventionMode"] = mode;
        return this;
    }

    public TestScenarioBuilder WithChannelName(string channelName)
    {
        _properties["ChannelName"] = channelName;
        return this;
    }

    public TestScenarioBuilder WithRequestData(object requestData)
    {
        _properties["RequestData"] = requestData;
        return this;
    }

    public ActionExecutingContext BuildActionContext()
    {
        var serviceName = _properties.GetValueOrDefault("ServiceName", "TestService") as string ?? "TestService";
        var methodName = _properties.GetValueOrDefault("MethodName", "ProcessRequestAsync") as string ?? "ProcessRequestAsync";
        var subject = _properties.GetValueOrDefault("Subject", "test.default") as string ?? "test.default";
        var conventionMode = _properties.GetValueOrDefault("ConventionMode", ConventionMode.RequestResponse);
        var requestData = _properties.GetValueOrDefault("RequestData");

        return TestFixtures.CreateActionExecutingContext(
            serviceName, methodName, subject, (ConventionMode)conventionMode, requestData);
    }
}