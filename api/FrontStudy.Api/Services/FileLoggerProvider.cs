/**
 * FileLoggerProvider.cs — 极简落盘日志（无第三方依赖）。
 *
 * 把 ILogger 输出追加写入 logs/api-{yyyyMMdd}.log，便于本地直接查看。
 * 学习用途，单进程低并发下足够；生产可换 Serilog。
 */
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FrontStudy.Api.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDir;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(logDir);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(_logDir));

    public void Dispose() => _loggers.Clear();
}

public sealed class FileLogger : ILogger
{
    private readonly string _logDir;
    private static readonly object Lock = new();

    public FileLogger(string logDir) => _logDir = logDir;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {message}"
                   + (exception is null ? string.Empty : $"{Environment.NewLine}{exception}");
        var file = Path.Combine(_logDir, $"api-{DateTime.Now:yyyyMMdd}.log");

        lock (Lock)
        {
            File.AppendAllText(file, line + Environment.NewLine);
        }
    }
}
