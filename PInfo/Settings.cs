/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Stores user preferences in the application's private persistent storage.
/// </summary>
public class Settings
{
    // ● private
    static readonly JsonSerializerOptions fJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<AppTheme>() }
    };
    readonly SemaphoreSlim fSaveLock = new(1, 1);

    /// <summary>
    /// Returns the settings path without creating directories.
    /// </summary>
    static string GetFilePath()
    {
        string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(Root))
            throw new IOException("The application data directory is unavailable.");
        return Path.Combine(Root, "PInfo", "settings.json");
    }

    // ● public
    /// <summary>
    /// Loads preferences, using Classic when the file is unavailable, invalid, or contains an unknown theme.
    /// </summary>
    /// <returns>The saved settings or default preferences.</returns>
    static public Settings Load()
    {
        try
        {
            Settings Result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(GetFilePath()), fJsonOptions);
            if (Result != null && Enum.IsDefined(Result.Theme))
                return Result;
        }
        catch (IOException)
        {
            // Missing or inaccessible storage must not prevent application startup.
        }
        catch (UnauthorizedAccessException)
        {
            // Missing or inaccessible storage must not prevent application startup.
        }
        catch (JsonException)
        {
            // Invalid preferences fall back to the default theme.
        }
        return new Settings();
    }
    /// <summary>
    /// Saves a snapshot through a temporary file, preserving the previous file when writing fails.
    /// </summary>
    /// <returns>True when saved; false if storage is unavailable or the selected theme is invalid.</returns>
    public async Task<bool> SaveAsync()
    {
        await fSaveLock.WaitAsync().ConfigureAwait(false);
        string TemporaryPath = null;
        try
        {
            if (!Enum.IsDefined(Theme))
                return false;
            string Json = JsonSerializer.Serialize(this, fJsonOptions);
            string FilePath = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(TemporaryPath, Json).ConfigureAwait(false);
            File.Move(TemporaryPath, FilePath, true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (TemporaryPath != null)
                    File.Delete(TemporaryPath);
            }
            catch (IOException)
            {
                // Cleanup failure must not hide the save result.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup failure must not hide the save result.
            }
            fSaveLock.Release();
        }
    }

    // ● properties
    /// <summary>
    /// The preferred application theme. Classic is used when no preference has been saved.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Classic;
}
