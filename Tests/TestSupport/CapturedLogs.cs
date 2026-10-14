using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tests.TestSupport;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, string? Exception)
{
    public string AllText => $"{Message} {Exception}";
}

public sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();
    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string category) => new Logger(category, _entries);
    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new CapturedLog(category, level, formatter(state, exception), exception?.ToString()));
    }
}