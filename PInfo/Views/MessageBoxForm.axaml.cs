/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Defines the buttons and purpose of a message box.
/// </summary>
public enum MessageBoxMode
{
    /// <summary>Displays information with an OK button.</summary>
    Info,
    /// <summary>Displays an error with an OK button.</summary>
    Error,
    /// <summary>Asks a question with Yes and No buttons.</summary>
    YesNo
}

/// <summary>
/// Displays a full-page message and completes asynchronously when dismissed.
/// </summary>
public partial class MessageBoxForm : AppForm
{
    // ● private
    readonly TaskCompletionSource<bool> fCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    MessageBoxMode fMode;

    /// <summary>Shows a message on the active main view. Must be called on the UI thread.</summary>
    static Task<bool> Show(MessageBoxMode Mode, string Message, bool KeepLastView)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (MainView.Current == null)
            throw new InvalidOperationException("There is no active MainView.");

        MessageBoxForm Form = new();
        Form.fMode = Mode;
        Form.MessageText.Text = Message;
        switch (Mode)
        {
            case MessageBoxMode.Info:
                Form.ModeText.Text = "PInfo Information";
                break;
            case MessageBoxMode.Error:
                Form.ModeText.Text = "PInfo Error";
                break;
            case MessageBoxMode.YesNo:
                Form.ModeText.Text = "PInfo Question";
                break;
        }
        Form.btnYes.IsVisible = Mode == MessageBoxMode.YesNo;
        Form.btnNo.IsVisible = Mode == MessageBoxMode.YesNo;
        Form.btnOK.IsVisible = Mode != MessageBoxMode.YesNo;
        return MainView.Current.ShowMessageBox(Form, KeepLastView);
    }
    /// <summary>Completes the message with the response of the clicked button.</summary>
    void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender == btnYes || Sender == btnOK)
            fCompletion.TrySetResult(true);
        else if (Sender == btnNo)
            fCompletion.TrySetResult(false);
    }

    // ● constructor
    /// <summary>Creates the message view and connects its buttons.</summary>
    public MessageBoxForm()
    {
        InitializeComponent();
        btnOK.Click += AnyClick;
        btnYes.Click += AnyClick;
        btnNo.Click += AnyClick;
    }

    // ● public
    /// <summary>Displays information. Completes when OK or Back is pressed.</summary>
    /// <param name="Message">The message text.</param>
    /// <param name="KeepLastView">Restores the current form when true; otherwise opens a new service list.</param>
    /// <returns>A task completed after the previous layout is restored.</returns>
    static public Task Info(string Message, bool KeepLastView)
    {
        return Show(MessageBoxMode.Info, Message, KeepLastView);
    }
    /// <summary>Displays an error. Completes when OK or Back is pressed.</summary>
    /// <param name="Message">The message text.</param>
    /// <param name="KeepLastView">Restores the current form when true; otherwise opens a new service list.</param>
    /// <returns>A task completed after the previous layout is restored.</returns>
    static public Task Error(string Message, bool KeepLastView)
    {
        return Show(MessageBoxMode.Error, Message, KeepLastView);
    }
    /// <summary>Asks a question. Back has the same meaning as No.</summary>
    /// <param name="Message">The question text.</param>
    /// <param name="KeepLastView">Restores the current form when true; otherwise opens a new service list.</param>
    /// <returns>True for Yes; false for No or Back, after the previous layout is restored.</returns>
    static public Task<bool> YesNo(string Message, bool KeepLastView)
    {
        return Show(MessageBoxMode.YesNo, Message, KeepLastView);
    }
    /// <summary>Dismisses the message using its mode's Back response.</summary>
    internal void CloseFromBack()
    {
        fCompletion.TrySetResult(fMode != MessageBoxMode.YesNo);
    }

    // ● properties
    /// <summary>The user's response, completed by a button or Back.</summary>
    internal Task<bool> Completion => fCompletion.Task;
}
