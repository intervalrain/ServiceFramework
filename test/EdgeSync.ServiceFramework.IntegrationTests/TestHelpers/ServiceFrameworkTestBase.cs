using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EdgeSync.ServiceFramework.AspNetCore.Mvc;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.Core;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;

/// <summary>
/// Base class for Service Framework integration tests providing common test infrastructure
/// </summary>
public abstract class ServiceFrameworkTestBase : IDisposable
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IHost TestHost;
    private bool _disposed = false;

    protected ServiceFrameworkTestBase()
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureTestServices(services);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            });

        TestHost = hostBuilder.Build();
        ServiceProvider = TestHost.Services;
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Configure basic AutoConvention services needed for testing
        services.Configure<AutoConventionOptions>(options =>
        {
            options.UseExceptionHandler = true;
            options.Settings = new List<AutoConventionSetting>
            {
                new AutoConventionSetting { Assembly = typeof(TestNatsService).Assembly }
            };
        });

        // Add the Service Framework components
        services.AddAutoConvention();

        // Override NATS connection with test doubles
        services.AddSingleton<INatsConnectionFactory, TestNatsConnectionFactory>();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    protected T? GetService<T>()
    {
        return ServiceProvider.GetService<T>();
    }

    protected IEnumerable<T> GetServices<T>()
    {
        return ServiceProvider.GetServices<T>();
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            TestHost?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test NATS connection factory for integration tests
/// </summary>
public class TestNatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<TestNatsConnectionFactory> _logger;

    public TestNatsConnectionFactory(ILogger<TestNatsConnectionFactory> logger)
    {
        _logger = logger;
    }

    public async Task<INatsConnection> CreateConnectionAsync(string connectionName, NatsApiOptions natsApiOptions)
    {
        _logger.LogInformation("Creating test NATS connection for {ConnectionName}", connectionName);
        
        // Return a test NATS connection substitute
        var connection = Substitute.For<INatsConnection>();
        
        // Setup default behaviors for the test connection
        connection.ServerInfo.Returns(new NatsServerInfo("test-server", "2.9.0", "go1.19", "host", 4222, 8888, false, 0, [], []));
        
        return await Task.FromResult(connection);
    }

    public async Task<INatsConnection> CreateConnectionAsync(string connectionName, ServiceConfig config)
    {
        return await CreateConnectionAsync(connectionName, config.NatsApiOptions);
    }
}

/// <summary>
/// Test NATS service for integration testing
/// </summary>
[ServiceInfo(ServiceName = "TestService")]
public interface ITestNatsService
{
    [Subject("test.request-response")]
    Task<string> ProcessRequestAsync(string input);

    [Subject("test.pub-sub")]
    [JetStream]
    Task PublishEventAsync(string eventData);

    [Subject("test.parameterless")]
    Task<int> GetCountAsync();
}

/// <summary>
/// Test implementation of the NATS service
/// </summary>
public class TestNatsService : ITestNatsService
{
    private readonly ILogger<TestNatsService> _logger;

    public TestNatsService(ILogger<TestNatsService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ProcessRequestAsync(string input)
    {
        _logger.LogInformation("Processing request: {Input}", input);
        return await Task.FromResult($"Processed: {input}");
    }

    public async Task PublishEventAsync(string eventData)
    {
        _logger.LogInformation("Publishing event: {EventData}", eventData);
        await Task.CompletedTask;
    }

    public async Task<int> GetCountAsync()
    {
        _logger.LogInformation("Getting count");
        return await Task.FromResult(42);
    }
}