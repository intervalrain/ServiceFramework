
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Testlib;

public class MockLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new MockScope();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var logEntry = $"[{logLevel}] [{typeof(T).Name}] {message}";

        Console.WriteLine(logEntry);

        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception.Message}");
            Console.WriteLine($"Stack Trace: {exception.StackTrace}");
        }
    }

    private class MockScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}