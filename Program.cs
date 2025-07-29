using System;
using System.Diagnostics;
using Avalonia;
using DesktopNotifications;
using Microsoft.Extensions.DependencyInjection;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using urlhandler.DependencyInjection;

namespace urlhandler;

internal class Program
{
  public static INotificationManager NotificationManager = null!;
  public static IServiceProvider ServiceProvider = null!;

  private static void Main(string[] args)
  {
    // setup DI container
    var services = new ServiceCollection();
    services.AddApplicationServices();
    ServiceProvider = services.BuildServiceProvider();
    ServiceLocator.Initialize(ServiceProvider);

    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  public static AppBuilder BuildAvaloniaApp()
  {
    IconProvider.Current.Register<FontAwesomeIconProvider>();

    if (
      Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 10
      || Environment.OSVersion.Platform == PlatformID.Unix
    )
    {
      return AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .SetupDesktopNotifications(out NotificationManager!)
        .LogToTrace();
    }
    else
    {
      return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
  }
}
