/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
namespace PInfo.Views;

public partial class MainView : UserControl
{
    
    readonly Stack<AppForm> fForms = new();
    MessageBoxForm fMessageBox;
    TopLevel fTopLevel;
    static bool fStartupNotificationShown;
    
    // ● private 
    async void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender == btnTest)
            await Test();
        else if (Sender == btnSettings && fMessageBox == null && fForms.Peek() is not SettingsForm)
            PushForm(new SettingsForm());
        else if (Sender == btnAbout && fMessageBox == null && fForms.Peek() is not AboutForm)
            PushForm(new AboutForm());
    }
    /// <summary>
    /// Tests the GeoNames import for Greece and displays its outcome in a full-page message.
    /// </summary>
    async Task Test()
    {
        if (!btnTest.IsEnabled || fMessageBox != null)
            return;
        btnTest.IsEnabled = false;
        object PreviousCaption = btnTest.Content;
        btnTest.Content = "Loading...";
        ApiClientResult Result;
        try
        {
            Result = await GeoNamesService.LoadPlacesAsync("GR");
        }
        catch (Exception Ex)
        {
            ExceptionLog.WriteException("MainView.Test", Ex, false);
            Result = new ApiClientResult(string.Empty)
            {
                ResultStatus = ApiClientResultStatus.Error,
                ReasonPhrase = "An unexpected error occurred while importing places for Greece."
            };
        }
        finally
        {
            btnTest.Content = PreviousCaption;
            btnTest.IsEnabled = true;
        }
        if (Result.ResultStatus == ApiClientResultStatus.Success)
        {
            string Message = string.IsNullOrWhiteSpace(Result.ReasonPhrase)
                ? "Places for Greece are available in the database."
                : Result.ReasonPhrase;
            await MessageBoxForm.Info(Message, true);
        }
        else if (Result.ResultStatus == ApiClientResultStatus.Cancelled)
            await MessageBoxForm.Info("The place load was cancelled.", true);
        else
            await MessageBoxForm.Error(Result.ReasonPhrase, true);
    }
    /// <summary>Shows the previous session's fatal error once, after the view has loaded.</summary>
    async void OnLoaded(object Sender, RoutedEventArgs Args)
    {
        if (fStartupNotificationShown || !GlobalExceptionHandler.HasPreviousFatalError)
            return;

        fStartupNotificationShown = true;
        await MessageBoxForm.Info(
            "The previous application session ended unexpectedly.", true);
        GlobalExceptionHandler.AcknowledgePreviousFatalErrors();
    }
    void OnBackRequested(object sender, RoutedEventArgs e)
    {
        if (fMessageBox != null)
        {
            fMessageBox.CloseFromBack();
            e.Handled = true;
            return;
        }
        // If we are currently displaying a secondary view, go back and handle the event
        if (GoBack())
        {
            e.Handled = true; // Prevent the app from exiting
        }
    }
    
    // ● overrides 
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Hook up to the system/hardware back button event for mobile
        fTopLevel = TopLevel.GetTopLevel(this);
        if (fTopLevel != null)
            fTopLevel.BackRequested += OnBackRequested;
        Current = this;
    }

    /// <summary>Removes the Back subscription when the main view is detached.</summary>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (fTopLevel != null)
            fTopLevel.BackRequested -= OnBackRequested;
        fTopLevel = null;
        if (Current == this)
            Current = null;
        base.OnDetachedFromVisualTree(e);
    }

    // ● construction 
    public MainView()
    {
        InitializeComponent();
        
        // Save the initial service list content of the container
        PushForm(new ServiceListForm());

        btnTest.Click += AnyClick;
        btnSettings.Click += AnyClick;
        btnAbout.Click += AnyClick;
        Loaded += OnLoaded;
    }
    
    // ● public
    /// <summary>Opens the registered service form on top of the navigation stack.</summary>
    /// <param name="Item">The service registration containing its form factory.</param>
    internal void OpenService(PublicServiceItem Item)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (fMessageBox != null)
            return;
        ArgumentNullException.ThrowIfNull(Item);
        PushForm(Item.CreateForm());
    }
    /// <summary>
    /// Pushes a form onto the navigation stack and displays it, retaining previous instances.
    /// Navigation is ignored while a message box is displayed.
    /// </summary>
    /// <param name="Form">A form instance not already present in the stack.</param>
    /// <exception cref="ArgumentException">The form is a message box or is already in the stack.</exception>
    public void PushForm(AppForm Form)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(Form);
        if (fMessageBox != null)
            return;
        if (Form is MessageBoxForm || fForms.Contains(Form))
            throw new ArgumentException("The form must be a new navigation entry, not a message box.", nameof(Form));

        fForms.Push(Form);
        ViewContainer.Content = Form;
    }
    /// <summary>Removes the top form and restores the preceding instance without removing the root.</summary>
    /// <returns>True when a form was removed; false at the root or while a message box is displayed.</returns>
    public bool GoBack()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (fMessageBox != null || fForms.Count <= 1)
            return false;

        fForms.Pop();
        ViewContainer.Content = fForms.Peek();
        return true;
    }
    /// <summary>Temporarily replaces the entire layout and restores it before returning the response.</summary>
    internal async Task<bool> ShowMessageBox(MessageBoxForm Form, bool KeepLastView)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (fMessageBox != null)
            throw new InvalidOperationException("A message box is already displayed.");

        fMessageBox = Form;
        try
        {
            RootContainer.Content = Form;
            return await Form.Completion;
        }
        finally
        {
            try
            {
                if (!KeepLastView)
                {
                    AppForm RootForm = new ServiceListForm();
                    fForms.Clear();
                    fForms.Push(RootForm);
                    ViewContainer.Content = RootForm;
                }
            }
            finally
            {
                RootContainer.Content = MainLayout;
                fMessageBox = null;
            }
        }
    }

    // ● properties
    /// <summary>The attached main view used to host full-page messages.</summary>
    internal static MainView Current { get; set; }
}
