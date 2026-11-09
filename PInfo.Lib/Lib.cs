/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Represents this library
/// </summary>
static public partial class Lib
{
    // ● private
    static readonly Lock fInitializeLock = new();
    static bool fInitialized;
    static CountryService fCountries;
    static Settings fSettings;
    static ReadOnlyCollection<PublicService> fPublicServices;

    // ● public
    /// <summary>
    /// Initializes this class. Repeated calls share the same service and settings instances.
    /// Does not download data or register platform-specific exception handlers.
    /// </summary>
    static public void Initialize()
    {
        lock (fInitializeLock)
        {
            if (fInitialized)
                return;

            ExceptionLog.Initialize();
            Settings Preferences = PInfo.Settings.Load();
            CountryService Countries = new();
            List<PublicService> Services = new() { Countries };
            fSettings = Preferences;
            fCountries = Countries;
            fPublicServices = Services.AsReadOnly();
            fInitialized = true;
        }
    }

    // ● properties
    /// <summary>
    /// The shared Countries service, created once for all clients in the process.
    /// </summary>
    static public CountryService Countries { get { Initialize(); return fCountries; } }
    /// <summary>
    /// The shared preferences loaded once from the existing private settings file.
    /// </summary>
    static public Settings Settings { get { Initialize(); return fSettings; } }
    /// <summary>
    /// The library's shared services, without UI form registrations.
    /// </summary>
    static public IReadOnlyList<PublicService> PublicServices { get { Initialize(); return fPublicServices; } }
}
