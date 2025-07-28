using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.Data;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Services;

/// <summary>
/// Unit tests for ResponseProcessor to verify UseExceptionHandler behavior
/// Tests the unwrapping of ErrorOr and ResponseDto types
/// </summary>
public class ResponseProcessorTests
{
    private readonly ILogger<ResponseProcessor> _mockLogger;
    private readonly ResponseProcessor _responseProcessor;

    public ResponseProcessorTests()
    {
        _mockLogger = Substitute.For<ILogger<ResponseProcessor>>();
        _responseProcessor = new ResponseProcessor(_mockLogger);
    }

    [Fact]
    public void ProcessResponse_WithUseExceptionHandlerTrue_UnwrapsResponseDto()
    {
        // Arrange
        var successData = new { Message = "Test data" };
        var responseDto = ResponseDto<object>.Success(successData, Guid.NewGuid());

        // Act
        var result = _responseProcessor.ProcessResponse(responseDto, useExceptionHandler: true);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBe(successData); // Should return unwrapped data
    }

    [Fact]
    public void ProcessResponse_WithUseExceptionHandlerFalse_KeepsFullResponseDto()
    {
        // Arrange
        var successData = new { Message = "Test data" };
        var responseDto = ResponseDto<object>.Success(successData, Guid.NewGuid());

        // Act
        var result = _responseProcessor.ProcessResponse(responseDto, useExceptionHandler: false);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBeOfType<ResponseDto<object>>(); // Should return full ResponseDto
    }

    [Fact]
    public void ProcessResponse_WithUseExceptionHandlerTrue_UnwrapsErrorOr()
    {
        // Arrange
        var successData = new { Message = "Test data" };
        ErrorOr<object> errorOr = successData; // Use implicit conversion

        // Act
        var result = _responseProcessor.ProcessResponse(errorOr, useExceptionHandler: true);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBe(successData); // Should return unwrapped value
    }

    [Fact]
    public void UnwrapResponse_WithSuccessfulResponseDto_ReturnsDataWith200()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var responseDto = ResponseDto<object>.Success(testData, Guid.NewGuid());

        // Act
        var result = _responseProcessor.UnwrapResponse(responseDto);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBe(testData);
    }

    [Fact]
    public void UnwrapResponse_WithFailedResponseDto_ReturnsErrorWith400()
    {
        // Arrange
        var error = Error.Validation("Validation.Failed", "Validation failed");
        var responseDto = ResponseDto<object>.Failure(error, Guid.NewGuid());

        // Act
        var result = _responseProcessor.UnwrapResponse(responseDto);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.ShouldNotBeNull();
        
        // Should contain error message
        var errorObj = badRequestResult.Value;
        var errorProperty = errorObj?.GetType().GetProperty("error");
        errorProperty?.GetValue(errorObj)?.ToString().ShouldBe("Validation failed");
    }

    [Fact]
    public void UnwrapResponse_WithSuccessfulErrorOr_ReturnsValueWith200()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        ErrorOr<object> errorOr = testData; // Use implicit conversion

        // Act
        var result = _responseProcessor.UnwrapResponse(errorOr);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBe(testData);
    }

    [Fact]
    public void UnwrapResponse_WithFailedErrorOr_ReturnsErrorWith400()
    {
        // Arrange
        var error = Error.Validation("Field.Required", "Field is required");
        ErrorOr<object> errorOr = error; // Use implicit conversion

        // Act
        var result = _responseProcessor.UnwrapResponse(errorOr);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.ShouldNotBeNull();
        
        // Should contain error message
        var errorObj = badRequestResult.Value;
        var errorProperty = errorObj?.GetType().GetProperty("error");
        errorProperty?.GetValue(errorObj)?.ToString().ShouldBe("Field is required");
    }

    [Fact]
    public void UnwrapResponse_WithNull_ReturnsOkResult()
    {
        // Act
        var result = _responseProcessor.UnwrapResponse(null);

        // Assert
        result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public void UnwrapResponse_WithRegularObject_ReturnsOkObjectResult()
    {
        // Arrange
        var testData = new { Message = "Regular object" };

        // Act
        var result = _responseProcessor.UnwrapResponse(testData);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBe(testData);
    }

    [Fact]
    public void GetResponseWithStatus_WithSuccessfulResponseDto_Returns200WithFullModel()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var responseDto = ResponseDto<object>.Success(testData, Guid.NewGuid());

        // Act
        var result = _responseProcessor.GetResponseWithStatus(responseDto);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.ShouldBeOfType<ResponseDto<object>>(); // Full ResponseDto, not just data
    }

    [Fact]
    public void GetResponseWithStatus_WithFailedResponseDto_Returns400WithFullModel()
    {
        // Arrange
        var error = Error.Validation("Validation.Failed", "Validation failed");
        var responseDto = ResponseDto<object>.Failure(error, Guid.NewGuid());

        // Act
        var result = _responseProcessor.GetResponseWithStatus(responseDto);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.ShouldBeOfType<ResponseDto<object>>(); // Full ResponseDto including error info
    }

    [Fact]
    public void GetResponseWithStatus_WithNull_Returns204NoContent()
    {
        // Act
        var result = _responseProcessor.GetResponseWithStatus(null);

        // Assert
        result.ShouldBeOfType<StatusCodeResult>();
        var statusResult = (StatusCodeResult)result;
        statusResult.StatusCode.ShouldBe(204);
    }
}