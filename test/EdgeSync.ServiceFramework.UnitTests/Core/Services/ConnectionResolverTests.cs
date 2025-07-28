using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NSubstitute;
using Shouldly;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Services;

/// <summary>
/// Unit tests for ConnectionResolver to verify connection resolution logic
/// Tests the priority-based connection selection and serializer registry retrieval
/// </summary>
public class ConnectionResolverTests
{
    private readonly IServiceProvider _mockServiceProvider;
    private readonly ILogger<ConnectionResolver> _mockLogger;
    private readonly INatsConnectionFactory _mockConnectionFactory;
    private readonly IOptions<ServiceFrameworkOptions> _mockOptions;
    private readonly ConnectionResolver _connectionResolver;
    private readonly INatsConnection _mockConnection;

    public ConnectionResolverTests()
    {
        _mockServiceProvider = Substitute.For<IServiceProvider>();
        _mockLogger = Substitute.For<ILogger<ConnectionResolver>>();
        _mockConnectionFactory = Substitute.For<INatsConnectionFactory>();
        _mockOptions = Substitute.For<IOptions<ServiceFrameworkOptions>>();
        _mockConnection = Substitute.For<INatsConnection>();

        _mockServiceProvider.GetService<IOptions<ServiceFrameworkOptions>>()
            .Returns(_mockOptions);

        _connectionResolver = new ConnectionResolver(
            _mockServiceProvider,
            _mockLogger,
            _mockConnectionFactory);
    }

    [Fact]
    public async Task GetConnectionAsync_WithNoOptions_UsesDefaultConnectionFactory()
    {
        // Arrange
        _mockServiceProvider.GetService<IOptions<ServiceFrameworkOptions>>()
            .Returns((IOptions<ServiceFrameworkOptions>?)null);
        
        _mockConnectionFactory.CreateConnectionAsync()
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("test-channel");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync();
    }

    [Fact]
    public async Task GetConnectionAsync_WithSpecificChannelName_UsesChannelSpecificSettings()
    {
        // Arrange
        var channelSettings = new NatsConnectionSettings { Url = "nats://specific:4222" };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "test-channel", channelSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://specific:4222"))
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("test-channel");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync(Arg.Any<NatsConnectionSettings>());
    }

    [Fact]
    public async Task GetConnectionAsync_WithDefaultConnection_UsesDefaultConnectionSettings()
    {
        // Arrange
        var defaultSettings = new NatsConnectionSettings { Url = "nats://default:4222" };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://default:4222"))
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("non-existent-channel");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync(Arg.Any<NatsConnectionSettings>());
    }

    [Fact]
    public async Task GetConnectionAsync_WithChannelPriority_PrefersChannelOverDefault()
    {
        // Arrange
        var channelSettings = new NatsConnectionSettings { Url = "nats://channel:4222" };
        var defaultSettings = new NatsConnectionSettings { Url = "nats://default:4222" };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "test-channel", channelSettings },
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://channel:4222"))
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("test-channel");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://channel:4222"));
        await _mockConnectionFactory.DidNotReceive().CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://default:4222"));
    }

    [Fact]
    public async Task GetConnectionAsync_WithNoMatchingConnections_UsesParameterlessFactory()
    {
        // Arrange
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            Connections = new Dictionary<string, NatsConnectionSettings>()
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync()
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("non-existent");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync();
    }

    [Fact]
    public async Task GetConnectionAsync_WithEmptyChannelName_UsesDefaultConnection()
    {
        // Arrange
        var defaultSettings = new NatsConnectionSettings { Url = "nats://default:4222" };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://default:4222"))
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync("");

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync(Arg.Any<NatsConnectionSettings>());
    }

    [Fact]
    public async Task GetConnectionAsync_WithNullChannelName_UsesDefaultConnection()
    {
        // Arrange
        var defaultSettings = new NatsConnectionSettings { Url = "nats://default:4222" };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync(Arg.Is<NatsConnectionSettings>(s => s.Url == "nats://default:4222"))
            .Returns(_mockConnection);

        // Act
        var result = await _connectionResolver.GetConnectionAsync(null);

        // Assert
        result.ShouldBe(_mockConnection);
        await _mockConnectionFactory.Received(1).CreateConnectionAsync(Arg.Any<NatsConnectionSettings>());
    }

    [Fact]
    public async Task GetConnectionAsync_WithException_ReturnsNullAndLogsError()
    {
        // Arrange
        var serviceFrameworkOptions = new ServiceFrameworkOptions();
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        _mockConnectionFactory.CreateConnectionAsync()
            .Returns(Task.FromException<INatsConnection?>(new Exception("Connection failed")));

        // Act
        var result = await _connectionResolver.GetConnectionAsync("test-channel");

        // Assert
        result.ShouldBeNull();
        // Verify that LogError was called - we don't need to check exact parameters
        _mockLogger.ReceivedWithAnyArgs(1).LogError(default(Exception), default(string), default(object?[]));
    }

    [Fact]
    public void GetSerializerForConnection_WithNoOptions_ReturnsDefaultSerializer()
    {
        // Arrange
        _mockServiceProvider.GetService<IOptions<ServiceFrameworkOptions>>()
            .Returns((IOptions<ServiceFrameworkOptions>?)null);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("test-channel");

        // Assert
        result.ShouldBe(NatsDefaultSerializerRegistry.Default);
    }

    [Fact]
    public void GetSerializerForConnection_WithChannelSpecificSettings_ReturnsChannelSerializer()
    {
        // Arrange
        var channelSerializer = Substitute.For<INatsSerializerRegistry>();
        var channelSettings = new NatsConnectionSettings 
        { 
            Url = "nats://test:4222",
            NatsSerializerRegistry = channelSerializer
        };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "test-channel", channelSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("test-channel");

        // Assert
        result.ShouldBe(channelSerializer);
    }

    [Fact]
    public void GetSerializerForConnection_WithDefaultConnection_ReturnsDefaultSerializer()
    {
        // Arrange
        var defaultSerializer = Substitute.For<INatsSerializerRegistry>();
        var defaultSettings = new NatsConnectionSettings 
        { 
            Url = "nats://default:4222",
            NatsSerializerRegistry = defaultSerializer
        };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("non-existent");

        // Assert
        result.ShouldBe(defaultSerializer);
    }

    [Fact]
    public void GetSerializerForConnection_WithFrameworkDefaultSerializer_ReturnsFrameworkDefault()
    {
        // Arrange
        var frameworkSerializer = Substitute.For<INatsSerializerRegistry>();
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultSerializerRegistry = frameworkSerializer,
            Connections = new Dictionary<string, NatsConnectionSettings>()
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("non-existent");

        // Assert
        result.ShouldBe(frameworkSerializer);
    }

    [Fact]
    public void GetSerializerForConnection_WithChannelPriority_PrefersChannelOverDefault()
    {
        // Arrange
        var channelSerializer = Substitute.For<INatsSerializerRegistry>();
        var defaultSerializer = Substitute.For<INatsSerializerRegistry>();
        
        var channelSettings = new NatsConnectionSettings 
        { 
            Url = "nats://channel:4222",
            NatsSerializerRegistry = channelSerializer
        };
        var defaultSettings = new NatsConnectionSettings 
        { 
            Url = "nats://default:4222",
            NatsSerializerRegistry = defaultSerializer
        };
        
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "test-channel", channelSettings },
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("test-channel");

        // Assert
        result.ShouldBe(channelSerializer);
    }

    [Fact]
    public void GetSerializerForConnection_WithEmptyChannelName_UsesDefaultConnection()
    {
        // Arrange
        var defaultSerializer = Substitute.For<INatsSerializerRegistry>();
        var defaultSettings = new NatsConnectionSettings 
        { 
            Url = "nats://default:4222",
            NatsSerializerRegistry = defaultSerializer
        };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection("");

        // Assert
        result.ShouldBe(defaultSerializer);
    }

    [Fact]
    public void GetSerializerForConnection_WithNullChannelName_UsesDefaultConnection()
    {
        // Arrange
        var defaultSerializer = Substitute.For<INatsSerializerRegistry>();
        var defaultSettings = new NatsConnectionSettings 
        { 
            Url = "nats://default:4222",
            NatsSerializerRegistry = defaultSerializer
        };
        var serviceFrameworkOptions = new ServiceFrameworkOptions
        {
            DefaultConnection = "default-conn",
            Connections = new Dictionary<string, NatsConnectionSettings>
            {
                { "default-conn", defaultSettings }
            }
        };
        _mockOptions.Value.Returns(serviceFrameworkOptions);

        // Act
        var result = _connectionResolver.GetSerializerForConnection(null);

        // Assert
        result.ShouldBe(defaultSerializer);
    }
}