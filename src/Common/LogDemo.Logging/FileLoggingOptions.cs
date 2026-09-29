using System;
using System.Collections.Generic;
using System.IO;

namespace LogDemo.Logging;

/// <summary>
/// Settings for the local file log, bound from the <c>FileLogging</c> configuration section.
/// Log <em>levels</em> are not configured here: they use the standard <c>Logging:LogLevel</c>
/// section so they can be changed while the app is running.
/// </summary>
public sealed class FileLoggingOptions
{
    public const string SectionName = "FileLogging";

    public const string DefaultOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (T{ThreadId}) {SourceContext}: {Message:lj} {Scope}{NewLine}{Exception}";

    internal const int MinRetentionDays = 1;
    internal const int MaxRetentionDays = 365;
    internal const int DefaultMaxFileSizeMB = 20;

    /// <summary>
    /// Log folder. Environment variables are expanded, e.g. <c>%LOCALAPPDATA%\Contoso\LogDemo\Logs</c>.
    /// When empty, <c>%LOCALAPPDATA%\{Company}\{Product}\Logs</c> is used.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>File name prefix, defaults to the application name. Retention only touches files with this prefix.</summary>
    public string? FilePrefix { get; set; }

    /// <summary>Session log files not written to for this many days are deleted on startup.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Guards against crash loops: keep at most this many old files regardless of age. 0 = unlimited.</summary>
    public int MaxRetainedFiles { get; set; } = 100;

    /// <summary>A session rolls over to <c>_001</c>, <c>_002</c>, ... once its file reaches this size.</summary>
    public int MaxFileSizeMB { get; set; } = DefaultMaxFileSizeMB;

    public string OutputTemplate { get; set; } = DefaultOutputTemplate;

    /// <summary>
    /// Clamps invalid values instead of throwing, because a typo in appsettings.json must not stop
    /// the app from starting (or from logging). Returns warnings to log once the logger exists.
    /// </summary>
    internal IReadOnlyList<string> Normalize(string applicationName)
    {
        List<string> warnings = new List<string>();

        if (RetentionDays < MinRetentionDays || RetentionDays > MaxRetentionDays)
        {
            int clamped = Math.Min(Math.Max(RetentionDays, MinRetentionDays), MaxRetentionDays);
            warnings.Add($"{SectionName}:{nameof(RetentionDays)}={RetentionDays} is outside [{MinRetentionDays}..{MaxRetentionDays}], using {clamped}.");
            RetentionDays = clamped;
        }

        if (MaxRetainedFiles < 0)
        {
            warnings.Add($"{SectionName}:{nameof(MaxRetainedFiles)}={MaxRetainedFiles} is negative, using 0 (unlimited).");
            MaxRetainedFiles = 0;
        }

        if (MaxFileSizeMB < 1)
        {
            warnings.Add($"{SectionName}:{nameof(MaxFileSizeMB)}={MaxFileSizeMB} is invalid, using {DefaultMaxFileSizeMB}.");
            MaxFileSizeMB = DefaultMaxFileSizeMB;
        }

        if (string.IsNullOrWhiteSpace(OutputTemplate))
        {
            OutputTemplate = DefaultOutputTemplate;
        }

        string prefix = string.IsNullOrWhiteSpace(FilePrefix) ? applicationName : FilePrefix!;
        FilePrefix = SanitizePrefix(prefix);

        return warnings;
    }

    private static string SanitizePrefix(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            // '_' separates the prefix from the timestamp, so keep it out of the prefix itself.
            if (chars[i] == '_' || Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '-';
            }
        }

        return chars.Length == 0 ? "app" : new string(chars);
    }
}
