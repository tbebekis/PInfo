/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Connects application exception events to the library's UI-independent logger.
/// </summary>
static public class GlobalExceptionHandler
{
    // ● private
    static readonly Lock fInitializeLock = new();
    static bool fInitialized;

    /// <summary>
    /// Observes an unhandled exception raised by the UI dispatcher.
    /// </summary>
    static void OnDispatcherUnhandledException(object Sender, DispatcherUnhandledExceptionEventArgs Args)
    {
        if (!Args.Handled)
            ExceptionLog.MarkFatalException();
        ExceptionLog.WriteException("Dispatcher.UIThread.UnhandledException", Args.Exception, false);
        // Leave Handled unchanged to preserve normal exception propagation.
    }
    /// <summary>
    /// Observes an unhandled managed exception, possibly before process termination.
    /// </summary>
    static void OnDomainUnhandledException(object Sender, UnhandledExceptionEventArgs Args)
    {
        if (Args.IsTerminating)
            ExceptionLog.MarkFatalException();
        ExceptionLog.WriteException("AppDomain.UnhandledException", Args.ExceptionObject, Args.IsTerminating);
        // This notification does not provide recovery from termination.
    }
    /// <summary>
    /// Observes a task exception that was not observed by its caller.
    /// </summary>
    static void OnUnobservedTaskException(object Sender, UnobservedTaskExceptionEventArgs Args)
    {
        ExceptionLog.WriteException("TaskScheduler.UnobservedTaskException", Args.Exception, false);
        // Leave the exception's observation state unchanged.
    }

    // ● public
    /// <summary>
    /// Registers the three exception handlers once before application UI initialization.
    /// </summary>
    static public void Initialize()
    {
        lock (fInitializeLock)
        {
            if (fInitialized)
                return;

            ExceptionLog.Initialize();
            Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            fInitialized = true;
        }
    }
    /// <summary>
    /// Acknowledges the previous-session fatal markers without clearing current-session markers.
    /// </summary>
    static public void AcknowledgePreviousFatalErrors()
    {
        ExceptionLog.AcknowledgePreviousFatalErrors();
    }

    // ● properties
    /// <summary>
    /// Whether startup found an unacknowledged fatal marker from a previous session.
    /// </summary>
    static public bool HasPreviousFatalError => ExceptionLog.HasPreviousFatalError;
}
