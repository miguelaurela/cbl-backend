using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CBL.IntegrationTests.Support;

/// <summary>Captura todo lo que la API registra en el log, para comprobar que nunca filtra datos sensibles.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => new Scope(lines, state?.ToString());

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lines.Enqueue($"[{logLevel}] {category}: {formatter(state, exception)} {exception}");
        }
    }

    private sealed class Scope : IDisposable
    {
        public Scope(ConcurrentQueue<string> lines, string? description)
        {
            if (description is not null)
            {
                lines.Enqueue($"[Scope] {description}");
            }
        }

        public void Dispose()
        {
        }
    }
}
