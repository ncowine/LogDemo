using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace LogDemo.Logging;

/// <summary>
/// Owns the application's <see cref="ILoggerFactory"/>. Created before the DI container so that
/// container construction, module loading and startup failures can be logged too.
/// </summary>
/// <remarks>
/// Apps only ever depend on <c>Microsoft.Extensions.Logging.ILogger&lt;T&gt;</c>. Serilog is an
/// implementation detail behind it (the file sink) and can be swapped without touching any app.
/// </remarks>
public sealed class LoggingHost : IDisposable
{
    private readonly ILogger logger;
    private readonly Serilog.Core.Logger serilog;
    private readonly ILoggingHostExtension? extension;
    private int disposed;

    private LoggingHost(
        ILoggerFactory loggerFactory,
        Serilog.Core.Logger serilog,
        ILoggingHostExtension? extension,
        LogSession session,
        FileLoggingOptions options)
    {
        LoggerFactory = loggerFactory;
        this.serilog = serilog;
        this.extension = extension;
        Session = session;
        Options = options;
        this.logger = loggerFactory.CreateLogger("LogDemo.Logging.LoggingHost");
    }

    public ILoggerFactory LoggerFactory { get; }

    public LogSession Session { get; }

    public FileLoggingOptions Options { get; }

    /// <param name="configuration">
    /// Root configuration. Uses <c>Logging</c> (standard level filters, hot-reloadable) and <c>FileLogging</c>;
    /// an extension may read its own section.
    /// </param>
    /// <param name="applicationName">Used as the default file prefix; passed on to the extension.</param>
    /// <param name="defaultDirectory">Used when <c>FileLogging:Directory</c> is empty.</param>
    /// <param name="applicationVersion">Passed on to the extension, see <see cref="AppPaths.Version"/>.</param>
    /// <param name="extension">
    /// Optional add-on such as central export, e.g. <c>OpenTelemetryExport.FromConfiguration</c>.
    /// </param>
    public static LoggingHost Create(
        IConfiguration configuration,
        string applicationName,
        string defaultDirectory,
        string? applicationVersion = null,
        Func<LoggingHostContext, ILoggingHostExtension>? extension = null)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // If Serilog itself fails (disk full, access denied) it reports here instead of throwing into the app.
        SelfLog.Enable(message => Debug.WriteLine("[Serilog] " + message));

        FileLoggingOptions options = new FileLoggingOptions();
        configuration.GetSection(FileLoggingOptions.SectionName).Bind(options);
        List<string> warnings = new List<string>(options.Normalize(applicationName));

        string requestedDirectory = ExpandDirectory(options.Directory, defaultDirectory);
        string directory = EnsureWritableDirectory(requestedDirectory, applicationName, out Exception? directoryError);

        LogSession session = CreateUniqueSession(directory, options.FilePrefix!);

        Serilog.Core.Logger serilog = new LoggerConfiguration()
            // Serilog accepts everything; Microsoft.Extensions.Logging filters by the
            // "Logging:LogLevel" section, which reloads when appsettings.json changes.
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.File(
                path: session.LogFilePath,
                outputTemplate: options.OutputTemplate,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                fileSizeLimitBytes: options.MaxFileSizeMB * 1024L * 1024L,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: null, // retention across sessions is handled by LogRetentionCleaner
                // Unbuffered: every event reaches the OS immediately, so nothing is lost if the
                // process is killed or crashes. A WPF app logs far too little for this to matter.
                buffered: false,
                shared: false)
            .CreateLogger();

        // An add-on must never stop the app: if it cannot start, the app runs with the file log only
        // and the reason is the first warning in it.
        ILoggingHostExtension? hostExtension = null;
        Exception? extensionError = null;
        if (extension is not null)
        {
            try
            {
                hostExtension = extension(new LoggingHostContext(configuration, applicationName, applicationVersion ?? "unknown", session, warnings));
            }
            catch (Exception ex)
            {
                extensionError = ex;
            }
        }

        ILoggerFactory factory;
        try
        {
            factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));

                // Not builder.AddSerilog(): that extension also adds a Trace filter rule for its provider,
                // which silently overrides the Logging:LogLevel configuration.
                // dispose: false because LoggerFactory never disposes provider *instances*; the host owns it.
                builder.AddProvider(new SerilogLoggerProvider(serilog, dispose: false));
                builder.AddDebug(); // Visual Studio Output window; no-op when no debugger is attached.

                hostExtension?.ConfigureLogging(builder);
            });
        }
        catch
        {
            // Release the session file, so the caller's crash handling (or a retry) is not locked out of it.
            hostExtension?.Dispose();
            serilog.Dispose();
            throw;
        }

        LoggingHost host = new LoggingHost(factory, serilog, hostExtension, session, options);

        if (directoryError is not null)
        {
            host.logger.DirectoryFallback(requestedDirectory, directory, directoryError);
        }

        if (extensionError is not null)
        {
            host.logger.ExtensionFailed(extensionError);
        }

        foreach (string warning in warnings)
        {
            host.logger.ConfigurationProblem(warning);
        }

        return host;
    }

    /// <summary>
    /// First lines of every log file: who/what/where. The first things support needs to know
    /// when someone sends a log.
    /// </summary>
    public void WriteSessionHeader(string applicationName, Assembly entryAssembly)
    {
        if (entryAssembly is null)
        {
            throw new ArgumentNullException(nameof(entryAssembly));
        }

        string version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? entryAssembly.GetName().Version?.ToString()
            ?? "unknown";

        int processId;
        using (Process process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        this.logger.SessionStarting(
            applicationName,
            version,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.OSDescription.Trim(),
            processId,
            Session.SessionId);
        this.logger.LogFileInfo(Session.LogFilePath, Options.RetentionDays);
        this.extension?.WriteSessionHeader(this.logger);
    }

    /// <summary>Last line of a normal session. Its absence in a log file means the process died.</summary>
    public void WriteSessionFooter(string applicationName, int exitCode)
    {
        this.logger.SessionEnding(applicationName, exitCode, (DateTimeOffset.Now - Session.StartedAt).TotalSeconds);
    }

    /// <summary>Deletes expired logs on a background thread so startup is never slowed down by disk I/O.</summary>
    public Task StartRetentionCleanup()
    {
        LogRetentionCleaner cleaner = new LogRetentionCleaner(LoggerFactory.CreateLogger<LogRetentionCleaner>());
        return Task.Run(() =>
        {
            try
            {
                cleaner.Clean(Session, Options.RetentionDays, Options.MaxRetainedFiles, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                this.logger.RetentionCrashed(ex);
            }
        });
    }

    /// <summary>
    /// Shuts down the extension (central export sends what is still queued, a couple of seconds at most),
    /// then flushes and closes the log file. Safe to call more than once and from any thread.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            LoggerFactory.Dispose();
            this.extension?.Dispose();
            this.serilog.Dispose(); // flushes and closes the file
        }
    }

    private static string ExpandDirectory(string? configured, string defaultDirectory)
    {
        string value = string.IsNullOrWhiteSpace(configured) ? defaultDirectory : configured!;
        value = Environment.ExpandEnvironmentVariables(value);

        // Relative paths are relative to the executable, not to the (unpredictable) working directory.
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(AppContext.BaseDirectory, value));
    }

    private static string EnsureWritableDirectory(string directory, string applicationName, out Exception? error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
        {
            error = ex;
            string fallback = Path.Combine(Path.GetTempPath(), applicationName, "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static LogSession CreateUniqueSession(string directory, string filePrefix)
    {
        int processId;
        using (Process process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        DateTimeOffset startedAt = DateTimeOffset.Now;
        LogSession session = new LogSession(sessionId, directory, filePrefix, startedAt, processId);

        // Two sessions in one process within the same second (e.g. an in-process restart or tests)
        // would otherwise share a file. Bump the timestamp until the name is free.
        for (int attempt = 1; File.Exists(session.LogFilePath) && attempt < 100; attempt++)
        {
            session = new LogSession(sessionId, directory, filePrefix, startedAt.AddSeconds(attempt), processId);
        }

        return session;
    }
}
