/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
namespace PInfo.Views;

public partial class MainView : UserControl
{
    
    object mDefaultContent = null;
    MessageBoxForm fMessageBox;
    TopLevel fTopLevel;
    static bool fStartupNotificationShown;
    
    // ● private 
    async void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender == btnTest)
            await Test();
    }
    /// <summary>Tests a full-page message and restoration of the current form.</summary>
    Task Test()
    {
        return MessageBoxForm.Info("This is a test message.", true);
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
        if (ViewContainer.Content is AppForm && ViewContainer.Content != mDefaultContent)
        {
            ViewContainer.Content = mDefaultContent;
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
        ViewContainer.Content = new ServiceListForm();
        mDefaultContent = ViewContainer.Content;

        btnTest.Click += AnyClick;
        Loaded += OnLoaded;
    }
    
    // ● public
    /// <summary>Opens the registered service form while retaining the initial service list for Back.</summary>
    /// <param name="Item">The service registration containing its form factory.</param>
    internal void OpenService(PublicServiceItem Item)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (fMessageBox != null)
            return;
        ArgumentNullException.ThrowIfNull(Item);
        ViewContainer.Content = Item.CreateForm();
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
                    ViewContainer.Content = new ServiceListForm();
                    mDefaultContent = ViewContainer.Content;
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
