/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

public partial class App : Application
{
    public override void Initialize()
    {
        GlobalExceptionHandler.Initialize();
        AvaloniaXamlLoader.Load(this);
        ThemeManager.Apply(AppHost.Settings.Theme);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();

        }

        base.OnFrameworkInitializationCompleted();
    }
}
