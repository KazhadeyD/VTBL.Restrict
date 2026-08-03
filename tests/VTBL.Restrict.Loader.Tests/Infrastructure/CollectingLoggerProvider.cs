using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace VTBL.Restrict.Loader.Tests.Infrastructure
{
    /// <summary>
    /// In-memory logger sink for E2E observability checks.
    /// </summary>
    public sealed class CollectingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<(string Category, LogLevel Level, string Message)> _entries =
            new ConcurrentBag<(string, LogLevel, string)>();

        public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries =>
            _entries.ToArray();

        public void Clear() => _entries.Clear();

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentBag<(string Category, LogLevel Level, string Message)> _entries;

            public CollectingLogger(
                string category,
                ConcurrentBag<(string Category, LogLevel Level, string Message)> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var message = formatter(state, exception) ?? string.Empty;
                _entries.Add((_category, logLevel, message));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose()
            {
            }
        }
    }
}
