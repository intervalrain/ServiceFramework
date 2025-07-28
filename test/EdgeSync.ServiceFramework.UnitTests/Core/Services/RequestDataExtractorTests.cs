using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using System.Reflection;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Services;

/// <summary>
/// Unit tests for RequestDataExtractor to verify request data extraction logic
/// Tests parameter extraction, audit wrapper handling, and error scenarios
/// </summary>
public class RequestDataExtractorTests
{
    private readonly IServiceProvider _mockServiceProvider;
    private readonly ILogger<RequestDataExtractor> _mockLogger;
    private readonly IOptions<AutoConventionOptions> _mockOptions;
    private readonly RequestDataExtractor _requestDataExtractor;

    public RequestDataExtractorTests()
    {
        _mockServiceProvider = Substitute.For<IServiceProvider>();
        _mockLogger = Substitute.For<ILogger<RequestDataExtractor>>();
        _mockOptions = Substitute.For<IOptions<AutoConventionOptions>>();
        
        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>()
            .Returns(_mockOptions);
        
        _requestDataExtractor = new RequestDataExtractor(_mockServiceProvider, _mockLogger);
    }

    private ActionExecutingContext CreateActionExecutingContext(
        MethodInfo? originalMethod = null,
        Dictionary<string, object?>? actionArguments = null)
    {
        var actionDescriptor = new ActionDescriptor();
        if (originalMethod != null)
        {
            actionDescriptor.Properties["OriginalMethod"] = originalMethod;
        }

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            actionDescriptor);

        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments ?? new Dictionary<string, object?>(),
            null);

        return context;
    }

    [Fact]
    public void ExtractRequestData_WithNoOriginalMethod_ReturnsNull()
    {
        // Arrange
        var context = CreateActionExecutingContext();

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithParameterlessMethod_ReturnsNull()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod))!;
        var options = new AutoConventionOptions { EnableAuditWrapper = false };
        _mockOptions.Value.Returns(options);

        var context = CreateActionExecutingContext(method);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithParameterlessMethodAndAuditWrapper_ReturnsRequestDto()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod))!;
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);

        var requestDto = RequestDto<object>.Create(new object(), "user1", "tenant1", "corr1");
        var actionArguments = new Dictionary<string, object?> { { "request", requestDto } };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeSameAs(requestDto);
    }

    [Fact]
    public void ExtractRequestData_WithSingleParameter_ReturnsParameterValue()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.SingleParameterMethod))!;
        var testData = new TestDto { Id = 1, Name = "Test" };
        var actionArguments = new Dictionary<string, object?> { { "data", testData } };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeSameAs(testData);
    }

    [Fact]
    public void ExtractRequestData_WithSingleParameterNotFound_ReturnsDictionaryWithNullValue()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.SingleParameterMethod))!;
        var actionArguments = new Dictionary<string, object?> { { "wrongName", "value" } };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldNotBeNull();
        var resultDict = result as Dictionary<string, object?>;
        resultDict.ShouldNotBeNull();
        resultDict["data"].ShouldBeNull(); // Parameter not found, so it's null
    }

    [Fact]
    public void ExtractRequestData_WithMultipleParameters_ReturnsDictionary()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.MultipleParameterMethod))!;
        var actionArguments = new Dictionary<string, object?> 
        { 
            { "id", 1 },
            { "name", "Test" }
        };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldNotBeNull();
        var resultDict = result as Dictionary<string, object?>;
        resultDict.ShouldNotBeNull();
        resultDict["id"].ShouldBe(1);
        resultDict["name"].ShouldBe("Test");
    }

    [Fact]
    public void ExtractRequestData_WithMultipleParametersSomeMissing_ReturnsPartialDictionary()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.MultipleParameterMethod))!;
        var actionArguments = new Dictionary<string, object?> 
        { 
            { "id", 1 }
            // missing "name" parameter
        };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldNotBeNull();
        var resultDict = result as Dictionary<string, object?>;
        resultDict.ShouldNotBeNull();
        resultDict["id"].ShouldBe(1);
        resultDict["name"].ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithParameterlessMethodAndNoAuditWrapper_ReturnsNull()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod))!;
        var options = new AutoConventionOptions { EnableAuditWrapper = false };
        _mockOptions.Value.Returns(options);

        var context = CreateActionExecutingContext(method);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithParameterlessMethodAndNoMatchingRequestDto_ReturnsNull()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod))!;
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);

        var actionArguments = new Dictionary<string, object?> 
        { 
            { "otherParam", "value" }
        };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithParameterlessMethodAndNonObjectRequestDto_ReturnsNull()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod))!;
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);

        var requestDto = RequestDto<TestDto>.Create(new TestDto(), "user1", "tenant1", "corr1");
        var actionArguments = new Dictionary<string, object?> { { "request", requestDto } };
        var context = CreateActionExecutingContext(method, actionArguments);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractRequestData_WithException_ReturnsNullAndLogsError()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.SingleParameterMethod))!;
        var context = CreateActionExecutingContext(method);
        
        // Create a context that will throw an exception when accessing ActionArguments
        var mockContext = Substitute.For<ActionExecutingContext>(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null);
        
        mockContext.ActionDescriptor.Properties["OriginalMethod"] = method;
        mockContext.When(x => _ = x.ActionArguments).Do(x => throw new Exception("Test exception"));

        // Act
        var result = _requestDataExtractor.ExtractRequestData(mockContext);

        // Assert
        result.ShouldBeNull();
        _mockLogger.Received(1).LogError(
            Arg.Any<Exception>(),
            "Error extracting request data from action context");
    }

    [Fact]
    public void ExtractRequestData_WithNoOptionsConfigured_ReturnsParameterValue()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.SingleParameterMethod))!;
        var testData = new TestDto { Id = 1, Name = "Test" };
        var actionArguments = new Dictionary<string, object?> { { "data", testData } };
        var context = CreateActionExecutingContext(method, actionArguments);

        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>()
            .Returns((IOptions<AutoConventionOptions>?)null);

        // Act
        var result = _requestDataExtractor.ExtractRequestData(context);

        // Assert
        result.ShouldBeSameAs(testData);
    }

    private class TestService
    {
        public void ParameterlessMethod() { }
        public void SingleParameterMethod(TestDto data) { }
        public void MultipleParameterMethod(int id, string name) { }
    }

    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}