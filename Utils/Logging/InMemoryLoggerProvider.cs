using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Jido.Utils.Logging
{
    public class LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string Category { get; init; } = "";
        public string Message { get; init; } = "";
    }

    // AI-generated, boo !
    public class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _buffer = new();
        private readonly object _lock = new();

        public event Action<LogEntry>? EntryAdded;

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, this);

        internal void AddEntry(LogEntry entry)
        {
            lock (_lock)
                _buffer.Add(entry);
            EntryAdded?.Invoke(entry);
        }

        public List<LogEntry> GetSnapshot()
        {
            lock (_lock)
                return new List<LogEntry>(_buffer);
        }

        public void Dispose()
        { }
    }

    internal class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly InMemoryLoggerProvider _provider;

        public InMemoryLogger(string categoryName, InMemoryLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception != null)
                message += Environment.NewLine + exception;

            _provider.AddEntry(
                new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = logLevel,
                    Category = _categoryName,
                    Message = message
                }
            );
        }
    }
}
