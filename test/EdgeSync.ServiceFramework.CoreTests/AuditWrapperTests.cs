using EdgeSync.ServiceFramework.Data;
using ErrorOr;
using Xunit;

namespace EdgeSync.ServiceFramework.CoreTests;

public class AuditWrapperTests
{
    [Fact]
    public void RequestDto_Should_Wrap_Data_With_Audit_Info()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42 };
        var userId = "user123";
        var tenantId = "tenant456";
        var correlationId = "corr789";

        // Act
        var requestDto = RequestDto<TestData>.Create(data)
            .AddMetadata("UserId", userId)
            .AddMetadata("TenantId", tenantId)
            .AddMetadata("CorrelationId", correlationId);

        // Assert
        Assert.Equal(data, requestDto.Data);
        Assert.Equal(userId, requestDto.Metadata!["UserId"]);
        Assert.Equal(tenantId, requestDto.Metadata!["TenantId"]);
        Assert.Equal(correlationId, requestDto.Metadata!["CorrelationId"]);
        Assert.NotEqual(Guid.Empty, requestDto.ReqSeqId);
        Assert.True(requestDto.Timestamp > 0);
    }

    [Fact]
    public void ResponseDto_Should_Wrap_Success_Result()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42 };
        var reqSeqId = Guid.NewGuid();
        var userId = "user123";
        var tenantId = "tenant456";
        var correlationId = "corr789";

        // Act
        var metadata = new Dictionary<string, string>
        {
            ["UserId"] = userId,
            ["TenantId"] = tenantId,
            ["CorrelationId"] = correlationId
        };
        var responseDto = ResponseDto<TestData>.Success(data, reqSeqId)
            .EnrichWith(metadata);

        // Assert
        Assert.True(responseDto.IsSuccess);
        Assert.Equal(data, responseDto.Data);
        Assert.Equal(userId, responseDto.Metadata!["UserId"]);
        Assert.Equal(tenantId, responseDto.Metadata!["TenantId"]);
        Assert.Equal(correlationId, responseDto.Metadata!["CorrelationId"]);
        Assert.Equal(reqSeqId, responseDto.ReqSeqId);
        Assert.NotEqual(Guid.Empty, responseDto.RspSeqId);
        Assert.True(responseDto.Timestamp > 0);
    }

    [Fact]
    public void ResponseDto_Should_Convert_From_ErrorOr_Success()
    {
        // Arrange
        var data = new TestData { Name = "Test", Value = 42 };
        ErrorOr<TestData> errorOr = data;

        // Act
        ResponseDto<TestData> responseDto = errorOr;

        // Assert
        Assert.True(responseDto.IsSuccess);
        Assert.Equal(data, responseDto.Data);
        Assert.Empty(responseDto.Errors);
    }

    [Fact]
    public void ResponseDto_Should_Convert_From_ErrorOr_Error()
    {
        // Arrange
        var error = Error.Validation("Test.Error", "Test error description");
        ErrorOr<TestData> errorOr = error;

        // Act
        ResponseDto<TestData> responseDto = errorOr;

        // Assert
        Assert.True(responseDto.IsError);
        Assert.Null(responseDto.Data);
        Assert.Single(responseDto.Errors);
        Assert.Equal(error, responseDto.FirstError);
    }

    private class TestData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}