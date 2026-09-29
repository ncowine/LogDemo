using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging;

/// <summary>
/// Deletes old session log files. Runs once per start (see <see cref="LoggingHost.StartRetentionCleanup"/>).
/// Only files matching <c>{prefix}_*.log</c> are considered, so a shared log folder is safe,
/// and the files of the current session are never touched.
/// </summary>
public sealed class LogRetentionCleaner
{
    private readonly ILogger<LogRetentionCleaner> logger;

    public LogRetentionCleaner(ILogger<LogRetentionCleaner> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RetentionResult Clean(LogSession session, int retentionDays, int maxRetainedFiles, DateTime utcNow)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        DirectoryInfo directory = new DirectoryInfo(session.LogDirectory);
        if (!directory.Exists)
        {
            return RetentionResult.Empty;
        }

        DateTime cutoff = utcNow.AddDays(-retentionDays);

        FileInfo[] candidates = directory
            .EnumerateFiles(session.FilePrefix + "_*.log", SearchOption.TopDirectoryOnly)
            .Where(f => !session.Owns(f.Name))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToArray();

        int kept = 0;
        int deleted = 0;
        int failed = 0;

        foreach (FileInfo file in candidates)
        {
            bool expired = file.LastWriteTimeUtc < cutoff;
            bool overLimit = maxRetainedFiles > 0 && kept >= maxRetainedFiles;

            if (!expired && !overLimit)
            {
                kept++;
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
                this.logger.RetentionFileDeleted(file.Name, file.LastWriteTimeUtc);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Usually another running instance still has it open. We'll try again next start.
                failed++;
                this.logger.RetentionFileDeleteFailed(file.Name, ex);
            }
        }

        RetentionResult result = new RetentionResult(candidates.Length, deleted, failed);
        this.logger.RetentionCompleted(session.LogDirectory, retentionDays, result.Scanned, result.Deleted, result.Failed);
        return result;
    }
}

public readonly struct RetentionResult
{
    public static readonly RetentionResult Empty = new RetentionResult(0, 0, 0);

    public RetentionResult(int scanned, int deleted, int failed)
    {
        Scanned = scanned;
        Deleted = deleted;
        Failed = failed;
    }

    public int Scanned { get; }

    public int Deleted { get; }

    public int Failed { get; }
}
