using System;
using System.IO;

namespace LogDemo.Logging;

/// <summary>Describes the log file of the current application run. Registered as a singleton.</summary>
public sealed class LogSession
{
    internal LogSession(string sessionId, string logDirectory, string filePrefix, DateTimeOffset startedAt, int processId)
    {
        SessionId = sessionId;
        LogDirectory = logDirectory;
        FilePrefix = filePrefix;
        StartedAt = startedAt;

        // One file per run: prefix_yyyyMMdd_HHmmss_pid. The pid keeps two instances started
        // in the same second apart without needing a shared (slower) file sink.
        FileBaseName = $"{filePrefix}_{startedAt:yyyyMMdd_HHmmss}_{processId}";
    }

    /// <summary>Short id written in the session header; handy when users report issues.</summary>
    public string SessionId { get; }

    public string LogDirectory { get; }

    public string FilePrefix { get; }

    /// <summary>File name without extension, e.g. <c>LogDemo-Net_20260929_101500_4242</c>.</summary>
    public string FileBaseName { get; }

    /// <summary>First file of this session. If the size limit is hit, Serilog continues in <c>{base}_001.log</c> etc.</summary>
    public string LogFilePath => Path.Combine(LogDirectory, FileBaseName + ".log");

    public DateTimeOffset StartedAt { get; }

    /// <summary>True when <paramref name="fileName"/> belongs to this session, including size-rolled parts.</summary>
    public bool Owns(string fileName) =>
        Path.GetFileName(fileName).StartsWith(FileBaseName, StringComparison.OrdinalIgnoreCase);
}
