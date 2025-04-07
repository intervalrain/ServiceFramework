// using System;
// using System.Text.Json.Nodes;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Logging;
// using Moq;
// using NATS.Client.Core;
// using NATS.Client.Services;
// using Xunit;

// namespace ShadowAgent.Infrastructure.Nats.Tests
// {
//     public class ServiceFrameworkTest
//     {
//         private readonly Mock<ILogger<ServiceFramework>> _mockLogger;
//         private readonly ServiceFramework _serviceFramework;

//         public ServiceFrameworkTest()
//         {
//             _mockLogger = new Mock<ILogger<ServiceFramework>>();
//             _serviceFramework = new ServiceFramework(_mockLogger.Object);
//         }

//         [Fact]
//         public void Connect_ShouldEstablishConnection()
//         {
//             // Act
//             _serviceFramework.Connect();

//             // Assert
//             Assert.True(_serviceFramework.IsConnected());
//         }

//         [Fact]
//         public void Disconnect_ShouldDisposeResources()
//         {
//             // Arrange
//             _serviceFramework.Connect();

//             // Act
//             _serviceFramework.Disconnect();

//             // Assert
//             Assert.False(_serviceFramework.IsConnected());
//         }

//         [Fact]
//         public void IsConnected_ShouldReturnFalse_WhenNotConnected()
//         {
//             // Act
//             var result = _serviceFramework.IsConnected();

//             // Assert
//             Assert.False(result);
//         }

//         [Fact]
//         public void StatsHandler_ShouldLogInformation()
//         {
//             // Arrange
//             var mockEndpoint = new Mock<INatsSvcEndpoint>();
//             var jsonNode = JsonNode.Parse("{\"state\":\"connected\"}");

//             // Act
//             _serviceFramework.StatsHandler(mockEndpoint.Object, jsonNode);

//             // Assert
//             _mockLogger.Verify(logger => logger.LogInformation(It.IsAny<string>()), Times.Once);
//         }

//         [Fact]
//         public async Task AddServiceSync_ShouldThrowException_WhenNotConnected()
//         {
//             // Act & Assert
//             await Assert.ThrowsAsync<Exception>(() => _serviceFramework.AddServiceSync("TestService", "1.0.0", "TestGroup"));
//         }

//         [Fact]
//         public async Task AddEndpointAsync_ShouldThrowException_WhenNotConnected()
//         {
//             // Act & Assert
//             await Assert.ThrowsAsync<Exception>(() => _serviceFramework.AddEndpointAsync<string>("TestEndpoint"));
//         }

//         [Fact]
//         public async Task ReplyAsync_ShouldSendReply()
//         {
//             // Arrange
//             var mockSvcMsg = new Mock<NatsSvcMsg<string>>();
//             var message = "TestMessage";

//             // Act
//             await _serviceFramework.ReplyAsync(mockSvcMsg.Object, message);

//             // Assert
//             mockSvcMsg.Verify(msg => msg.ReplyAsync(message), Times.Once);
//         }

//         [Fact]
//         public async Task ReplyErrorAsync_ShouldSendErrorReply()
//         {
//             // Arrange
//             var mockSvcMsg = new Mock<NatsSvcMsg<string>>();
//             var errorCode = 500;
//             var errorMessage = "Error occurred";
//             var errorData = "ErrorData";

//             // Act
//             await _serviceFramework.ReplyErrorAsync(mockSvcMsg.Object, errorCode, errorMessage, errorData);

//             // Assert
//             mockSvcMsg.Verify(msg => msg.ReplyErrorAsync(errorCode, errorMessage, errorData), Times.Once);
//         }
//     }
// }