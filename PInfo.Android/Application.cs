/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace PInfo.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }
    }
}