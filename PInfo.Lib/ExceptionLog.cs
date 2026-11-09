/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides UI-independent exception logging and previous-session fatal markers.
/// Writes exceptions to local files and limits log age and disk usage.
/// </summary>
static public class ExceptionLog
{
    // ● private
    static readonly Lock fInitializeLock = new();
    static bool fInitialized;
    static readonly Lock fLogLock = new();
    const long MaxFileBytes = 1024 * 1024;
    const long MaxTotalBytes = 10 * 1024 * 1024;
    const int RetentionDays = 14;
    const int MaxExceptionCharacters = 100000;
    static string fLogDirectory;
    static string fCurrentFile;
    static string fCurrentDay;
    static readonly string fSessionId = Guid.NewGuid().ToString("N");
    static string[] fPreviousFatalFiles = Array.Empty<string>();

    /// <summary>
    /// Creates the log directory in the application's private persistent storage.
    /// </summary>
    static void PrepareDirectory()
    {
        if (fLogDirectory == null)
        {
            string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(Root))
                throw new IOException("The application data directory is unavailable.");
            fLogDirectory = Path.Combine(Root, "PInfo", "Logs");
        }
        Directory.CreateDirectory(fLogDirectory);
    }
    /// <summary>
    /// Selects a daily log file with enough room for the pending record.
    /// </summary>
    static void SelectFile(string Day, int RecordBytes)
    {
        int Index = 0;
        string FilePath;
        do
        {
            FilePath = Path.Combine(fLogDirectory,
                "pinfo-" + Day + "-" + Index.ToString("D4", CultureInfo.InvariantCulture) + ".log");
            Index++;
        }
        while (File.Exists(FilePath) && new FileInfo(FilePath).Length + RecordBytes > MaxFileBytes);

        fCurrentFile = FilePath;
        fCurrentDay = Day;
    }
    /// <summary>
    /// Deletes expired logs and then the oldest logs until the disk budget is met.
    /// Reserves space for a pending record and preserves the active log file.
    /// </summary>
    static void ApplyRetention(DateTime Now, int ReservedBytes)
    {
        FileInfo[] Files = new DirectoryInfo(fLogDirectory).GetFiles("pinfo-*.log")
            .OrderBy(Item => Item.LastWriteTimeUtc).ToArray();
        long TotalBytes = Files.Sum(Item => Item.Length);
        DateTime Cutoff = Now.AddDays(-RetentionDays);
        foreach (FileInfo Item in Files)
        {
            if (Item.FullName == fCurrentFile)
                continue;
            if (Item.LastWriteTimeUtc < Cutoff || TotalBytes + ReservedBytes > MaxTotalBytes)
            {
                try
                {
                    long Length = Item.Length;
                    Item.Delete();
                    TotalBytes -= Length;
                }
                catch
                {
                    // Continue with other files if an individual log cannot be deleted.
                }
            }
        }
    }

    // ● public
    /// <summary>
    /// Persists a session-specific fatal marker outside the log retention directory.
    /// </summary>
    static public void MarkFatalException()
    {
        try
        {
            lock (fLogLock)
            {
                PrepareDirectory();
                string FilePath = Path.Combine(Path.GetDirectoryName(fLogDirectory), "fatal-" + fSessionId + ".pending");
                using (FileStream Stream = new(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    byte[] Bytes = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    Stream.Write(Bytes, 0, Bytes.Length);
                    Stream.Flush(true);
                }
            }
        }
        catch
        {
            // A marker failure must not interfere with exception propagation or logging.
        }
    }
    /// <summary>
    /// Writes a bounded exception record synchronously. Logging failures never escape this method.
    /// </summary>
    static public void WriteException(string Source, object Error, bool IsTerminating)
    {
        try
        {
            lock (fLogLock)
            {
                PrepareDirectory();
                DateTime Now = DateTime.UtcNow;
                string Details = Error?.ToString() ?? "No exception details available.";
                if (Details.Length > MaxExceptionCharacters)
                    Details = Details.Substring(0, MaxExceptionCharacters) + "\n[Exception details truncated]";

                string Record = $"UTC: {Now:O}\nSource: {Source}\nIsTerminating: {IsTerminating}\n{Details}\n\n";
                byte[] Bytes = Encoding.UTF8.GetBytes(Record);
                string Day = Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                bool Rotate = fCurrentFile == null || fCurrentDay != Day
                    || !File.Exists(fCurrentFile)
                    || new FileInfo(fCurrentFile).Length + Bytes.Length > MaxFileBytes;
                if (Rotate)
                {
                    SelectFile(Day, Bytes.Length);
                    // Avoid directory cleanup while the process is terminating.
                    if (!IsTerminating)
                        ApplyRetention(Now, Bytes.Length);
                }

                using (FileStream Stream = new(fCurrentFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    Stream.Write(Bytes, 0, Bytes.Length);
                    Stream.Flush(true);
                }
            }
        }
        catch
        {
            // Disk, formatting, or cleanup failures must not cause another unhandled exception.
        }
    }
    /// <summary>
    /// Initializes previous-session markers and log retention once per process.
    /// Repeated calls have no effect.
    /// </summary>
    static public void Initialize()
    {
        lock (fInitializeLock)
        {
            if (fInitialized)
                return;

            fInitialized = true;

            try
            {
                lock (fLogLock)
                {
                    PrepareDirectory();
                    string CurrentMarker = "fatal-" + fSessionId + ".pending";
                    fPreviousFatalFiles = Directory.GetFiles(Path.GetDirectoryName(fLogDirectory), "fatal-*.pending")
                        .Where(Item => Path.GetFileName(Item) != CurrentMarker).ToArray();
                    ApplyRetention(DateTime.UtcNow, 0);
                }
            }
            catch
            {
                // Storage failures must not prevent the application from initializing.
            }
        }
    }
    /// <summary>
    /// Acknowledges markers found at startup, preserving any marker written by the current session.
    /// Failed deletions remain pending for the next startup.
    /// </summary>
    static public void AcknowledgePreviousFatalErrors()
    {
        lock (fLogLock)
        {
            foreach (string FilePath in fPreviousFatalFiles)
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch
                {
                    // Retry at the next startup if the marker cannot be removed.
                }
            }
            fPreviousFatalFiles = fPreviousFatalFiles.Where(File.Exists).ToArray();
        }
    }

    // ● properties
    /// <summary>
    /// Whether startup found an unacknowledged fatal marker from a previous session.
    /// </summary>
    static public bool HasPreviousFatalError
    {
        get
        {
            lock (fLogLock)
                return fPreviousFatalFiles.Length > 0;
        }
    }

}
