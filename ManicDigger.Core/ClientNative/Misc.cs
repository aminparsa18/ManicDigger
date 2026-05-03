using Serilog;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Crash reporter backed by Serilog.
/// Drop-in replacement for the original CrashReporter — same public API,
/// same usage pattern in ManicDiggerProgram.
///
/// NuGet packages required:
///   Serilog
///   Serilog.Sinks.File
///   Serilog.Sinks.Console   (optional, for console output)
/// </summary>
public class CrashReporter
{
    // ── Configuration ────────────────────────────────────────────────────────

    /// <summary>Default crash log filename (no path — stored under GameStorePath).</summary>
    public static string DefaultFileName { get; set; } = "ManicDiggerCrash.txt";

    /// <summary>
    /// Maximum time (ms) to wait for the OnCrash delegate before aborting.
    /// Prevents a hung cleanup callback from blocking shutdown forever.
    /// </summary>
    public static int OnCrashTimeoutMs { get; set; } = 5_000;

    // ── State ────────────────────────────────────────────────────────────────

    private static bool s_isConsole;
    private static ILogger s_globalLogger = Serilog.Core.Logger.None;

    private readonly ILogger m_logger;
    private readonly string m_crashFilePath;

    /// <summary>Optional cleanup hook — called (with timeout) before shutdown.</summary>
    public Action? OnCrash { get; set; }

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Writes crash report to <see cref="DefaultFileName"/> under the game store path.</summary>
    public CrashReporter() : this(DefaultFileName) { }

    /// <summary>Writes crash report to <paramref name="fileName"/> under the game store path.</summary>
    public CrashReporter(string fileName)
    {
        string dir = GameStorePath.GetStorePath();

        m_crashFilePath = Path.Combine(dir, fileName);

        m_logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: m_crashFilePath,
                rollingInterval: RollingInterval.Month,      // one file per month, auto-rotated
                retainedFileCountLimit: 6,                     // keep last 6 months
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)                        // safe for multi-thread append
            .CreateLogger();
    }

    // ── Global exception handling ─────────────────────────────────────────────

    /// <summary>
    /// Registers a global unhandled-exception handler.
    /// Call once from Main(), before anything else runs.
    /// </summary>
    /// <param name="isConsole">
    ///   true  → errors printed to stdout.<br/>
    ///   false → errors shown in a MessageBox.
    /// </param>
    public static void EnableGlobalExceptionHandling(bool isConsole)
    {
        s_isConsole = isConsole;

        // Build a lightweight global logger (console / debug) for the static handler.
        // The per-instance file logger is created when CrashReporter() is newed up.
        LoggerConfiguration cfg = new LoggerConfiguration().MinimumLevel.Fatal();

        if (isConsole)
        {
            cfg = cfg.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        s_globalLogger = cfg.CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (s_isConsole)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Unhandled exception — application is terminating.");
            Console.ResetColor();
        }

        Exception? ex = e.ExceptionObject as Exception;
        s_globalLogger.Fatal(ex, "Unhandled exception");

        // Create a full crash report using a default instance
        new CrashReporter().Crash(ex);
    }

    // ── Core crash logic ──────────────────────────────────────────────────────

    /// <summary>
    /// Logs the exception, displays an error to the user, then exits the process.
    /// </summary>
    public void Crash(Exception? exCrash)
    {
        // 1. Log to file via Serilog
        m_logger.Fatal(exCrash, "Critical error — application is terminating");

        // Walk inner exceptions so each gets its own structured entry
        for (Exception? inner = exCrash?.InnerException; inner != null; inner = inner.InnerException)
        {
            m_logger.Fatal(inner, "Inner exception");
        }

        // 2. Run caller-supplied cleanup (with timeout)
        RunOnCrashCallback();

        // 3. Flush — critical for async sinks so nothing is lost before Exit()
        (m_logger as IDisposable)?.Dispose();

        // 4. Tell the user
        string summary = BuildSummary(exCrash);
        DisplayToUser(summary);

        if (s_isConsole)
        {
            Console.WriteLine("Press any key to shut down...");
            Console.ReadLine();
        }

        Environment.Exit(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RunOnCrashCallback()
    {
        if (OnCrash == null)
        {
            return;
        }

        // Run on a separate thread so we can enforce a hard timeout
        Task task = Task.Run(() => OnCrash());
        if (!task.Wait(OnCrashTimeoutMs))
        {
            m_logger.Warning("OnCrash() did not complete within {Timeout} ms — skipped", OnCrashTimeoutMs);
        }

        if (task.IsFaulted)
        {
            m_logger.Error(task.Exception, "OnCrash() threw an exception");
        }
    }

    private string BuildSummary(Exception? ex)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  Critical Error");

        if (ex != null)
        {
            sb.AppendLine(ex.Message);
        }

        sb.AppendLine();
        sb.AppendLine($"Crash report written to:\n  {m_crashFilePath}");
        return sb.ToString();
    }

    private static void DisplayToUser(string message)
    {
        if (s_isConsole)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            // Nudge the cursor visible in case the game hid it
            for (int i = 0; i < 3; i++)
            {
                Cursor.Show();
                Thread.Sleep(50);
                Application.DoEvents();
            }

            MessageBox.Show(message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public static class GameStorePath
{
    public static bool IsMono = Type.GetType("Mono.Runtime") != null;

    public static string GetStorePath()
    {
        string apppath = Path.GetDirectoryName(Application.ExecutablePath);
        DirectoryInfo di = new(apppath);
        if (di.Name.Equals("AutoUpdaterTemp", StringComparison.InvariantCultureIgnoreCase))
        {
            apppath = di.Parent.FullName;
        }

        string mdfolder = "UserData";
        if (apppath.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) && !IsMono)
        {
            string mdpath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                mdfolder);
            return mdpath;
        }
        else
        {
            return Path.Combine(apppath, mdfolder);
        }
    }

    public static string gamepathconfig = Path.Combine(GetStorePath(), "Configuration");
    public static string gamepathsaves = Path.Combine(GetStorePath(), "Saves");
    public static string gamepathbackup = Path.Combine(GetStorePath(), "Backup");

    public static bool IsValidName(string s)
    {
        if (s.Length is < 1 or > 32)
        {
            return false;
        }

        for (int i = 0; i < s.Length; i++)
        {
            if (!AllowedNameChars.Contains(s[i].ToString()))
            {
                return false;
            }
        }

        return true;
    }
    public static string AllowedNameChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-";
}

public interface ICompression
{
    byte[] Compress(ReadOnlySpan<byte> data);
    byte[] Decompress(byte[] data);
}

/// <summary>
/// GZip-based implementation of <see cref="ICompression"/>.
///
/// Changes vs. previous version
/// ─────────────────────────────
/// 1. COMPRESSION LEVEL — defaults to <see cref="CompressionLevel.Fastest"/>.
///    The previous code used no explicit level, which defaults to
///    <see cref="CompressionLevel.Optimal"/> — the slowest mode, maximising
///    compression ratio at the expense of CPU time.  For real-time chunk
///    streaming the trade-off is wrong: chunks are written once and read
///    infrequently, so CPU time during generation matters far more than
///    saving a few hundred bytes per chunk.
///    <see cref="CompressionLevel"/> is exposed as a property so callers
///    can override it for offline batch operations where ratio matters.
///
/// 2. PRE-SIZED OUTPUT STREAMS — both Compress and Decompress now pass an
///    initial capacity to their MemoryStream, avoiding repeated internal
///    array resizes on typical chunk data:
///      Compress:   pre-sized to the input length (compressed output is always
///                  smaller than or equal to input for block data)
///      Decompress: pre-sized to input × 4 (block data compresses roughly 3–5×;
///                  × 4 avoids resizes in the common case without over-allocating)
///
/// 3. CopyTo REPLACES MANUAL BUFFER LOOP — the hand-written 4096-byte read
///    loop in Decompress is replaced with GZipStream.CopyTo(outputStream).
///    The BCL implementation buffers internally and the JIT can optimise it
///    more aggressively than user-land byte-shuffling code.
///
/// 4. FLATTENED USING DECLARATIONS — the nested using-inside-using in Decompress
///    is replaced with two top-level using declarations, removing one
///    level of indentation with no behaviour change.
/// </summary>
public class CompressionGzip : ICompression
{
    /// <summary>
    /// Controls the compression speed/ratio trade-off.
    /// Defaults to <see cref="CompressionLevel.Fastest"/> for real-time use.
    /// Set to <see cref="CompressionLevel.SmallestSize"/> for offline batch
    /// operations where storage size is the priority.
    /// </summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;

    /// <inheritdoc/>
    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        // Pre-size to input length — compressed output is always ≤ input for
        // structured block data, so this avoids all internal resize steps.
        MemoryStream output = new(data.Length);

        // GZipStream must be disposed before ToArray() so the final GZIP block
        // (checksum + end-of-stream marker) is flushed into output.
        using (GZipStream gzip = new(output, Level, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    /// <inheritdoc/>
    public byte[] Decompress(byte[] data)
    {
        using MemoryStream inStream = new(data, writable: false);
        using GZipStream gzip = new(inStream, CompressionMode.Decompress);

        // Pre-size to 4× compressed length — block data typically compresses
        // 3–5×, so this avoids resizes in the common case.
        MemoryStream output = new(data.Length * 4);
        gzip.CopyTo(output);
        return output.ToArray();
    }
}