/// <summary>
/// Main application class for ChemLocalLink
/// Manages app lifecycle and startup logic
/// Sets up dependency injection and services
/// Ensures only one instance runs at a time
/// Handles chemotion:// URL arguments
/// Initializes main window and view model
/// </summary>

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChemLocalLink.Extensions;
using ChemLocalLink.Helpers;
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
      var windowHelper = _serviceProvider.GetRequiredService<IWindowHelper>();

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
