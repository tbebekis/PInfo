/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace PInfo.Android;

[Activity(
    Label = "PInfo.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}