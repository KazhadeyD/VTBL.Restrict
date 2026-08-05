using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace VTBL.Restrict.Loader.Application.Tests.Support
{
    /// <summary>
    /// In-memory logger для unit/E2E проверки correlation logging (включая BeginScope).
    /// </summary>
    public sealed class CollectingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<(string Category, LogLevel Level, string Message)> _entries =
            new ConcurrentBag<(string, LogLevel, string)>();

        private readonly AsyncLocal<Stack<object>> _scopeStack = new AsyncLocal<Stack<object>>();

        public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries =>
            _entries.ToArray();

        public void Clear() => _entries.Clear();

        public ILogger CreateLogger(string categoryName) =>
            new CollectingLogger(categoryName, _entries, _scopeStack);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentBag<(string Category, LogLevel Level, string Message)> _entries;
            private readonly AsyncLocal<Stack<object>> _scopeStack;

            public CollectingLogger(
                string category,
                ConcurrentBag<(string Category, LogLevel Level, string Message)> entries,
                AsyncLocal<Stack<object>> scopeStack)
            {
                _category = category;
                _entries = entries;
                _scopeStack = scopeStack;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                var stack = _scopeStack.Value;
                if (stack == null)
                {
                    stack = new Stack<object>();
                    _scopeStack.Value = stack;
                }

                stack.Push(state);
                return new PopScope(stack);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var message = formatter(state, exception) ?? string.Empty;
                var scopeText = FormatScopes();
                if (!string.IsNullOrEmpty(scopeText))
                {
                    message = string.IsNullOrEmpty(message)
                        ? scopeText
                        : message + " | " + scopeText;
                }

                if (exception != null)
                {
                    message = message + " " + exception;
                }

                _entries.Add((_category, logLevel, message));
            }

            private string FormatScopes()
            {
                var stack = _scopeStack.Value;
                if (stack == null || stack.Count == 0)
                {
                    return string.Empty;
                }

                var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var snapshot = stack.ToArray();
                for (var i = snapshot.Length - 1; i >= 0; i--)
                {
                    MergeScope(values, snapshot[i]);
                }

                if (values.Count == 0)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var pair in values)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(pair.Key).Append('=').Append(pair.Value);
                }

                return sb.ToString();
            }

            private static void MergeScope(IDictionary<string, object> values, object scope)
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> objectPairs)
                {
                    foreach (var pair in objectPairs)
                    {
                        values[pair.Key] = pair.Value;
                    }

                    return;
                }

                if (scope is IEnumerable pairs)
                {
                    foreach (var item in pairs)
                    {
                        if (item is KeyValuePair<string, object> objectPair)
                        {
                            values[objectPair.Key] = objectPair.Value;
                        }
                        else if (item is KeyValuePair<string, string> stringPair)
                        {
                            values[stringPair.Key] = stringPair.Value;
                        }
                    }
                }
            }

            private sealed class PopScope : IDisposable
            {
                private readonly Stack<object> _stack;
                private bool _disposed;

                public PopScope(Stack<object> stack)
                {
                    _stack = stack;
                }

                public void Dispose()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    if (_stack.Count > 0)
                    {
                        _stack.Pop();
                    }
                }
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
