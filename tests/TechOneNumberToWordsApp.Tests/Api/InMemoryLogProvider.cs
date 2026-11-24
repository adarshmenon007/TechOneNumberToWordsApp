using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>One thing the application logged, with everything a logging test needs to look at (D33).</summary>
public sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    IReadOnlyList<object?> Scopes,
    Exception? Exception);

/// <summary>
/// Keeps every log entry in memory so a test can read it back. Test tooling only, no package (D45).
/// It implements <see cref="ISupportExternalScope"/> so the scopes it records are the host's own, including the
/// TraceId that the 500 test compares with the response (C2).
/// </summary>
public sealed class InMemoryLogProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private IExternalScopeProvider? _scopes;

    /// <summary>A snapshot of everything logged so far, oldest first.</summary>
    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, this);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, InMemoryLogProvider owner) : ILogger
    {
        // The host records scopes on the shared scope provider itself, so there is nothing to do here.
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<object?>();
            owner._scopes?.ForEachScope((scope, list) => list.Add(scope), scopes);

            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            owner._entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), values, scopes, exception));
        }
    }
}
