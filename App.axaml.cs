/// <summary>
/// Main application class
/// </summary>

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChemLocalLink.Services;
using ChemLocalLink.Utilities;
using ChemLocalLink.ViewModels;
using ChemLocalLink.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ChemLocalLink;

public class App : Application
{
  private IServiceProvider? _serviceProvider;

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      // configure DI with the notification manager
      var services = new ServiceCollection();
      services.AddApplicationServices(Program.NotificationManager);
      _serviceProvider = services.BuildServiceProvider();

      var mw = new MainWindowView();
      var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
      var windowService = _serviceProvider.GetRequiredService<IWindowService>();

      viewModel.Initialize(mw, desktop.Args ?? []);

      mw.DataContext = viewModel;
      desktop.Startup += DesktopOnStartup;
      desktop.MainWindow = mw;
      desktop.MainWindow.DataContext = mw.DataContext;
    }

    base.OnFrameworkInitializationCompleted();
  }

  private void DesktopOnStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
  {
    var currentProcess = Process.GetCurrentProcess();
    if (Process.GetProcessesByName("ChemLocalLink").Length > 0)
    {
      var processes = Process.GetProcessesByName("ChemLocalLink");
      foreach (Process process in processes)
      {
        if (process.Id != currentProcess.Id)
        {
          try
          {
            process.Kill();
          }
          catch (Exception ex)
          {
            Debug.WriteLine($"Failed to kill process {process.Id}: {ex.Message}");
          }
        }
      }
    }
  }
}
