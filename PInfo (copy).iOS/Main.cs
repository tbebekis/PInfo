/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
using UIKit;

namespace PInfo.iOS;

public class Application
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}