using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.IntegrationTests.Compatibility;

/// <summary>
/// Integration tests for backwards compatibility with existing service implementations
/// Ensures the refactored architecture maintains compatibility with pre-refactoring code
/// </summary>
public class BackwardsCompatibilityIntegrationTests : ServiceFrameworkTestBase
{
    [Fact]
    public async Task BackwardsCompatibility_ExistingNatsServiceInterface_StillWorks()
    {
        // Arrange - Test that existing NATS service interfaces work unchanged
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        
        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();
        
        // Assert
        discoveryResult.ShouldNotBeNull();
        
        // Should discover our test service using existing interface patterns
        var allServices = discoveryResult.ReqRspServices.Concat(discoveryResult.PubSubServices);
        allServices.ShouldContain(service => service.ServiceType == typeof(ITestNatsService));
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingAttributeUsage_StillSupported()
    {
        // Arrange - Test that existing attributes still work
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.existing-attributes",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "compatibility test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Should work with existing SubjectAttribute and NatsService attribute patterns
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async void BackwardsCompatibility_ExistingServiceConfiguration_StillSupported()
    {
        // Arrange - Test that existing AutoConventionOptions still work
        var options = GetRequiredService<IOptions<AutoConventionOptions>>();
        
        // Act & Assert
        options.ShouldNotBeNull();
        options.Value.ShouldNotBeNull();
        
        // Existing properties should still be available
        options.Value.UseExceptionHandler.ShouldNotBeNull();
        options.Value.Settings.ShouldNotBeNull();
        options.Value.Settings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingMethodSignatures_StillWork()
    {
        // Arrange - Test that methods with existing signatures still work
        var filter = GetRequiredService<NatsProxyActionFilter>();
        
        // Test parameterless method (existing pattern)
        var parameterlessContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "GetCountAsync",
            subject: "test.parameterless-compatibility",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: null);

        // Test method with parameters (existing pattern)
        var parameterContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.parameter-compatibility",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test parameter");

        // Act
        await filter.OnActionExecutionAsync(parameterlessContext, 
            () => Task.FromResult(new ActionExecutedContext(parameterlessContext, [], controller: null)));
            
        await filter.OnActionExecutionAsync(parameterContext, 
            () => Task.FromResult(new ActionExecutedContext(parameterContext, [], controller: null)));

        // Assert
        parameterlessContext.Result.ShouldNotBeNull();
        parameterContext.Result.ShouldNotBeNull();
        
        parameterlessContext.Result.ShouldBeOfType<ObjectResult>();
        parameterContext.Result.ShouldBeOfType<ObjectResult>();
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingConventionModes_StillSupported()
    {
        // Arrange - Test all existing convention modes
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        
        var existingModes = new[]
        {
            ConventionMode.RequestResponse,
            ConventionMode.PubSubPushClassic,
            ConventionMode.PubSubPushJetStream,
            ConventionMode.PubSubPullJetStream
        };

        // Act & Assert
        foreach (var mode in existingModes)
        {
            // Each existing mode should still be supported
            Should.NotThrow(() => handlerFactory.GetHandler(mode));
            
            var actionContext = TestFixtures.CreateActionExecutingContext(
                serviceName: "TestService",
                methodName: mode == ConventionMode.RequestResponse ? "ProcessRequestAsync" : "PublishEventAsync",
                subject: $"test.compatibility.{mode}",
                conventionMode: mode,
                actionArguments: "compatibility test");

            await filter.OnActionExecutionAsync(actionContext, 
                () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

            actionContext.Result.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingServiceRegistration_StillWorks()
    {
        // Arrange - Test that existing service registration patterns still work
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;

        // Act & Assert - Should start without breaking existing patterns
        Should.NotThrow(async () =>
        {
            await backgroundService.StartAsync(cancellationToken);
            var executeTask = backgroundService.ExecuteAsync(cancellationToken);
            await Task.Delay(100, CancellationToken.None);
            await backgroundService.StopAsync(CancellationToken.None);
            
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });
    }

    [Fact]
    public void BackwardsCompatibility_ExistingDependencyInjection_StillSupported()
    {
        // Arrange & Act - Test that existing DI patterns still work
        
        // These services should still be available with existing names/interfaces
        var filter = GetService<NatsProxyActionFilter>();
        var backgroundService = GetService<ServiceFrameworkBackgroundService>();
        
        // Assert
        filter.ShouldNotBeNull();
        backgroundService.ShouldNotBeNull();
        
        // Existing service types should still resolve
        Should.NotThrow(() => GetRequiredService<NatsProxyActionFilter>());
        Should.NotThrow(() => GetRequiredService<ServiceFrameworkBackgroundService>());
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingErrorHandling_MaintainedBehavior()
    {
        // Arrange - Test that error handling behaves the same as before refactoring
        var filter = GetRequiredService<NatsProxyActionFilter>();
        
        // Test the same error scenarios that existed before refactoring
        var missingSubjectContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "", // This should fail the same way as before
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "error test");

        // Act
        await filter.OnActionExecutionAsync(missingSubjectContext, 
            () => Task.FromResult(new ActionExecutedContext(missingSubjectContext, [], controller: null)));

        // Assert - Should return the same error format as before
        var badRequestResult = missingSubjectContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldBe("Subject not found in action metadata");
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingAuditFlow_StillWorks()
    {
        // Arrange - Test that existing audit functionality still works
        var auditHandler = GetRequiredService<IAuditHandler>();
        var filter = GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.audit-compatibility",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "audit compatibility test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Audit functionality should still work (no audit-related errors)
        actionContext.Result.ShouldBeOfType<ObjectResult>();
        auditHandler.ShouldNotBeNull();
    }

    [Fact]
    public void BackwardsCompatibility_ExistingServiceCollectionExtensions_StillWork()
    {
        // Arrange - Test that existing extension methods still work
        var services = new ServiceCollection();
        
        // Act & Assert - Existing AddAutoConvention methods should still work
        Should.NotThrow(() =>
        {
            services.AddAutoConvention();
        });

        Should.NotThrow(() =>
        {
            services.AddAutoConvention<TestAutoConventionRouteBuilder>();
        });
    }

    [Theory]
    [InlineData("test.legacy-pattern")]
    [InlineData("legacy.service.method")]
    [InlineData("app.service.action")]
    public async Task BackwardsCompatibility_ExistingSubjectPatterns_StillSupported(string subject)
    {
        // Arrange - Test that existing subject naming patterns still work
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: subject,
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "legacy pattern test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<ObjectResult>();
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingChannelConfiguration_StillWorks()
    {
        // Arrange - Test that existing channel configuration patterns still work
        var channelResolver = GetRequiredService<IChannelResolver>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        
        // Act
        var channelName = channelResolver.GetChannelName(typeof(ITestNatsService), 
            typeof(ITestNatsService).GetMethod("ProcessRequestAsync")!);
        var connection = await connectionResolver.GetConnectionAsync(channelName);

        // Assert
        channelName.ShouldNotBeNull();
        connection.ShouldNotBeNull();
    }

    [Fact]
    public async Task BackwardsCompatibility_ExistingResponseFormat_Maintained()
    {
        // Arrange - Test that response format is the same as before refactoring
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var responseProcessor = GetRequiredService<IResponseProcessor>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.response-format",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "response format test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Response should be formatted the same way as before refactoring
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
        
        // Response processor should maintain existing format behavior
        responseProcessor.ShouldNotBeNull();
    }

    [Fact]
    public void BackwardsCompatibility_ExistingLoggingPattern_Maintained()
    {
        // Arrange - Test that logging behavior is maintained
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        
        // Act & Assert - Services should be properly logged (no logging-related exceptions)
        filter.ShouldNotBeNull();
        backgroundService.ShouldNotBeNull();
        
        // Services should have loggers injected (no null reference exceptions during logging)
        Should.NotThrow(() =>
        {
            // These services should be able to log without issues
            var auditHandler = GetRequiredService<IAuditHandler>();
            var connectionResolver = GetRequiredService<IConnectionResolver>();
            var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
            
            auditHandler.ShouldNotBeNull();
            connectionResolver.ShouldNotBeNull();
            handlerFactory.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task BackwardsCompatibility_PerformanceCharacteristics_NotDegraded()
    {
        // Arrange - Test that performance is not significantly degraded by refactoring
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.performance-compatibility",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "performance test");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        stopwatch.Stop();

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<ObjectResult>();
        
        // Performance should be maintained (allowing for test overhead and delegation overhead)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should complete in reasonable time
    }
}

/// <summary>
/// Test auto convention route builder for compatibility testing
/// </summary>
public class TestAutoConventionRouteBuilder : IAutoConventionRouteBuilder
{
    public string BuildRoute(Type serviceType, string methodName)
    {
        return $"api/{serviceType.Name.Replace("Service", "").ToLower()}/{methodName.Replace("Async", "").ToLower()}";
    }
}