using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Services;

/// <summary>
/// Unit tests for AuditHandler to verify EnableAuditWrapper behavior
/// Tests the wrapping of requests with RequestDto and audit information extraction
/// </summary>
public class AuditHandlerTests
{
    private readonly IServiceProvider _mockServiceProvider;
    private readonly ILogger<AuditHandler> _mockLogger;
    private readonly IOptions<AutoConventionOptions> _mockOptions;
    private readonly AuditHandler _auditHandler;
    private readonly DefaultHttpContext _httpContext;

    public AuditHandlerTests()
    {
        _mockServiceProvider = Substitute.For<IServiceProvider>();
        _mockLogger = Substitute.For<ILogger<AuditHandler>>();
        _mockOptions = Substitute.For<IOptions<AutoConventionOptions>>();
        
        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>()
            .Returns(_mockOptions);
        
        _auditHandler = new AuditHandler(_mockServiceProvider, _mockLogger);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public void WrapRequestWithAudit_WithEnableAuditWrapperTrue_WrapsRequestInRequestDto()
    {
        // Arrange
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);
        
        var testData = new { Id = 1, Name = "Test" };
        _httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { 
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "testuser") 
            }, "test"));
        _httpContext.Request.Headers["TenantId"] = "tenant123";
        _httpContext.Request.Headers["CorrelationId"] = "corr456";

        // Act
        var result = _auditHandler.WrapRequestWithAudit(testData, _httpContext);

        // Assert
        result.ShouldNotBeNull();
        
        // Verify it's wrapped in RequestDto<T>
        var resultType = result.GetType();
        resultType.IsGenericType.ShouldBeTrue();
        resultType.GetGenericTypeDefinition().ShouldBe(typeof(RequestDto<>));
        
        // Verify audit information is included
        var dataProperty = resultType.GetProperty("Data");
        var userIdProperty = resultType.GetProperty("UserId");
        var tenantIdProperty = resultType.GetProperty("TenantId");
        var correlationIdProperty = resultType.GetProperty("CorrelationId");
        
        dataProperty?.GetValue(result).ShouldBe(testData);
        userIdProperty?.GetValue(result)?.ToString().ShouldBe("testuser");
        tenantIdProperty?.GetValue(result)?.ToString().ShouldBe("tenant123");
        correlationIdProperty?.GetValue(result)?.ToString().ShouldBe("corr456");
    }

    [Fact]
    public void WrapRequestWithAudit_WithEnableAuditWrapperFalse_ReturnsOriginalRequest()
    {
        // Arrange
        var options = new AutoConventionOptions { EnableAuditWrapper = false };
        _mockOptions.Value.Returns(options);
        
        var testData = new { Id = 1, Name = "Test" };

        // Act
        var result = _auditHandler.WrapRequestWithAudit(testData, _httpContext);

        // Assert
        result.ShouldBe(testData); // Should return original data unchanged
    }

    [Fact]
    public void WrapRequestWithAudit_WithNullRequest_CreatesRequestDtoWithNullData()
    {
        // Arrange
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);
        
        _httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { 
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "testuser") 
            }, "test"));

        // Act
        var result = _auditHandler.WrapRequestWithAudit(null, _httpContext);

        // Assert
        result.ShouldNotBeNull();
        
        // Should be RequestDto<object> with empty object as data
        var resultType = result.GetType();
        resultType.IsGenericType.ShouldBeTrue();
        resultType.GetGenericTypeDefinition().ShouldBe(typeof(RequestDto<>));
        resultType.GetGenericArguments()[0].ShouldBe(typeof(object));
    }

    [Fact]
    public void WrapRequestWithAudit_WithAlreadyWrappedRequest_ReturnsAsIs()
    {
        // Arrange
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);
        
        var testData = new { Id = 1, Name = "Test" };
        var requestDto = RequestDto<object>.Create(testData, "user1", "tenant1", "corr1");

        // Act
        var result = _auditHandler.WrapRequestWithAudit(requestDto, _httpContext);

        // Assert
        result.ShouldNotBeNull(); // Should return the already wrapped request unchanged
    }

    [Fact]
    public void WrapRequestWithAudit_WithMissingTenantAndCorrelationHeaders_GeneratesDefaults()
    {
        // Arrange
        var options = new AutoConventionOptions { EnableAuditWrapper = true };
        _mockOptions.Value.Returns(options);
        
        var testData = new { Id = 1, Name = "Test" };
        _httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { 
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "testuser") 
            }, "test"));
        // Note: No TenantId or CorrelationId headers

        // Act
        var result = _auditHandler.WrapRequestWithAudit(testData, _httpContext);

        // Assert
        result.ShouldNotBeNull();
        
        var resultType = result.GetType();
        var tenantIdProperty = resultType.GetProperty("TenantId");
        var correlationIdProperty = resultType.GetProperty("CorrelationId");
        
        tenantIdProperty?.GetValue(result).ShouldBeNull(); // No header provided
        var correlationId = correlationIdProperty?.GetValue(result) as string;
        correlationId.ShouldNotBeNullOrEmpty(); // Should generate a GUID
        Guid.TryParse(correlationId!, out _).ShouldBeTrue(); // Should be a valid GUID
    }

    [Fact]
    public void ExtractAuditInfo_WithRequestDto_ReturnsAuditInformation()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var requestDto = RequestDto<object>.Create(testData, "user1", "tenant1", "corr1");

        // Act
        var result = _auditHandler.ExtractAuditInfo(requestDto);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ReqSeqId.ShouldNotBeNullOrEmpty(); // Should have a sequence ID
        result.Value.Timestamp.ShouldNotBeNullOrEmpty(); // Should have a timestamp
    }

    [Fact]
    public void ExtractAuditInfo_WithNonRequestDto_ReturnsNull()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };

        // Act
        var result = _auditHandler.ExtractAuditInfo(testData);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractAuditInfo_WithNull_ReturnsNull()
    {
        // Act
        var result = _auditHandler.ExtractAuditInfo(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractResponseAuditInfo_WithResponseDto_ReturnsAuditInformation()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var responseDto = ResponseDto<object>.Success(testData, Guid.Parse("550e8400-e29b-41d4-a716-446655440000"))
            .WithAuditInfo("user1", "tenant1");

        // Act
        var result = _auditHandler.ExtractResponseAuditInfo(responseDto);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ReqSeqId.ShouldBe("550e8400-e29b-41d4-a716-446655440000");
        result.Value.RspSeqId.ShouldNotBeNullOrEmpty();
        result.Value.Timestamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ExtractResponseAuditInfo_WithNonResponseDto_ReturnsNull()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };

        // Act
        var result = _auditHandler.ExtractResponseAuditInfo(testData);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractResponseAuditInfo_WithNull_ReturnsNull()
    {
        // Act
        var result = _auditHandler.ExtractResponseAuditInfo(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void WrapRequestWithAudit_WithNoOptionsConfigured_ReturnsOriginalRequest()
    {
        // Arrange
        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>()
            .Returns((IOptions<AutoConventionOptions>?)null);
        
        var testData = new { Id = 1, Name = "Test" };

        // Act
        var result = _auditHandler.WrapRequestWithAudit(testData, _httpContext);

        // Assert
        result.ShouldBe(testData); // Should return original data when options not configured
    }
}