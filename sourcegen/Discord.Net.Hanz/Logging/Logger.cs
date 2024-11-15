using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;

namespace Discord.Net.Hanz;

public sealed class Logger : ILogger, IEquatable<Logger>, IDisposable
{
    public const int MAX_UNFLUSHED_LOGS = 2500;

    public static readonly string LogDirectory = Path.Combine(Environment.CurrentDirectory, ".hanz");
    private readonly ConcurrentDictionary<string, Logger> _subLoggers = [];

    private readonly LogLevel _logLevel;
    private readonly string _logFilePath;

    private readonly LinkedList<string> _logs;

    private readonly object _syncRoot = new();

    private readonly bool _autoFlush;

    private Func<string?> _prefix;

    public string GetPrefix(LogLevel logLevel) => _prefix?.Invoke() ?? $"[{DateTime.Now:O} | {logLevel}]";

    public Logger(
        LogLevel logLevel,
        string logFilePath,
        bool autoFlush = true)
    {
        _autoFlush = autoFlush;
        _logs = new();
        _logLevel = logLevel;
        _logFilePath = logFilePath;

        var dir = Path.GetDirectoryName(logFilePath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private sealed class PrefixScope : IDisposable
    {
        private readonly Logger _logger;
        private readonly Func<string?>? _oldPrefix;

        public PrefixScope(Logger logger, Func<string?> prefix)
        {
            _logger = logger;
            _oldPrefix = logger._prefix;
            logger._prefix = prefix;
        }

        public void Dispose()
        {
            _logger._prefix = _oldPrefix;
        }
    }

    private sealed class FlushScope : IDisposable
    {
        private readonly Logger _logger;

        public FlushScope(Logger logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            _logger.Flush();
        }
    }

    private FlushScope? _flushScope;

    public IDisposable GetFlushScope()
        => _flushScope ??= new(this);

    public IDisposable WithPrefix(string prefix)
        => new PrefixScope(this, () => prefix);

    public IDisposable WithPrefix(Func<string?> prefix)
        => new PrefixScope(this, prefix);

    public Logger GetSubLogger(string name)
        => _subLoggers.GetOrAdd(
            name,
            name =>
            {
                var logger = new Logger(
                    _logLevel,
                    Path.Combine(
                        Path.GetDirectoryName(_logFilePath) ?? LogDirectory,
                        Path.GetFileNameWithoutExtension(_logFilePath),
                        $"{name}.subtask.log"
                    ),
                    _autoFlush
                );
                logger.DeleteLogFile();
                return logger;
            }
        );

    public Logger WithCleanLogFile(bool clearLogs = true)
    {
        DeleteLogFile(clearLogs);
        return this;
    }

    public void Clean(bool clearLogs = true)
    {
        DeleteLogFile(clearLogs);
        foreach (var subLogger in _subLoggers.ToArray())
        {
            subLogger.Value.Clean(clearLogs);
        }
    }

    public Logger WithSemanticContext(SemanticModel model)
        => WithCompilationContext(model.Compilation);

    public Logger WithCompilationContext(Compilation compilation)
    {
        if (_logFilePath.Contains(compilation.Assembly.Name))
            return this;

        var name = Path.Combine(
            LogDirectory,
            compilation.Assembly.Name,
            Path.GetFileName(_logFilePath)
        );

        return _subLoggers.GetOrAdd(name, name =>
        {
            var logger = new Logger(_logLevel, name, _autoFlush);
            logger.DeleteLogFile();
            return logger;
        });
    }

    public void DeleteLogFile(bool clearLogs = true)
    {
        if (_logFilePath.ToLowerInvariant().Contains("roslyn"))
            return;

        lock (_syncRoot)
        {
            try
            {
                if (clearLogs) _logs.Clear();
                if (File.Exists(_logFilePath))
                    File.Delete(_logFilePath);
            }
            catch
            {
                // ignored
            }
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _logLevel;
    }

    public void Log(LogLevel logLevel, string message)
    {
        if (!IsEnabled(logLevel))
            return;

        if (_logFilePath.ToLowerInvariant().Contains("roslyn"))
            return;

        try
        {
            lock (_syncRoot)
            {
                _logs.AddLast($"{GetPrefix(logLevel)} {message}");

                if (_autoFlush && _logs.Count >= MAX_UNFLUSHED_LOGS)
                    NoLockFlush();
            }
        }
        catch (Exception ex)
        {
            SelfLog.Write(ex.ToString());
        }
    }

    public void Flush()
    {
        lock (_syncRoot)
        {
            NoLockFlush();
        }
    }

    private void NoLockFlush()
    {
        if (_logs.Count >= 0)
        {
            using (var fs = File.Open(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(fs))
            {
                foreach (var log in _logs)
                    writer.WriteLine(log);

                writer.Flush();
                fs.Flush();
            }

            _logs.Clear();
        }

        foreach (var subLogger in _subLoggers)
            subLogger.Value.Flush();
    }

    public static Logger CreateSemanticRun(string assembly)
    {
        return new Logger(Hanz.LoggerOptions.Level,
            Path.Combine(LogDirectory, assembly, "latest.log"));
    }

    public static Logger CreateSemanticRunForTask(string assembly, string task, bool autoFlush = true)
    {
        return new Logger(
            Hanz.LoggerOptions.Level,
            Path.Combine(LogDirectory, assembly, $"{task}.task.log"),
            autoFlush
        );
    }

    public static Logger CreateForTask(string task, bool autoFlush = true)
    {
        return new Logger(
            Hanz.LoggerOptions.Level,
            Path.Combine(LogDirectory, $"{task}.task.log"),
            autoFlush
        );
    }

    public bool Equals(Logger? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _logLevel == other._logLevel && _logFilePath == other._logFilePath;
    }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is Logger other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int) _logLevel * 397) ^ _logFilePath.GetHashCode();
        }
    }

    void IDisposable.Dispose()
    {
        Flush();
    }

    public static bool operator ==(Logger? left, Logger? right) => Equals(left, right);

    public static bool operator !=(Logger? left, Logger? right) => !Equals(left, right);
}