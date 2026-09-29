using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Synapse.Blocks.Api.Tests.Infrastructure;

internal sealed class CapturingLoggerProvider(ConcurrentQueue<string> entries) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries, categoryName);
    public void Dispose() { }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue($"{categoryName}: {formatter(state, exception)}");
    }
}
