using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace VTBL.Restrict.Application.Tests.Support
{
    /// <summary>
    /// In-memory logger для unit/E2E проверки correlation logging.
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
                var message = formatter(state, exception);
                if (exception != null)
                {
                    message = message + " " + exception;
                }

                _entries.Add((_category, logLevel, message ?? string.Empty));
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

    public sealed class CollectingLogger<T> : ILogger<T>
    {
        private readonly CollectingLoggerProvider _provider;
        private readonly ILogger _inner;

        public CollectingLogger(CollectingLoggerProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _inner = provider.CreateLogger(typeof(T).FullName);
        }

        public CollectingLoggerProvider Provider => _provider;

        public IDisposable BeginScope<TState>(TState state) => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter) =>
            _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
