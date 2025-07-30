using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChemLocalLink.DependencyInjection;
using ChemLocalLink.Helpers;
using ChemLocalLink.ViewModels;
using ChemLocalLink.Views;

namespace ChemLocalLink;

public class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var mw = new MainWindow();
      var viewModel = ServiceLocator.GetService<MainWindowViewModel>();
      viewModel.Initialize(mw, desktop.Args ?? []);

      WindowHelper.MainWindowViewModel = viewModel;
      mw.DataContext = viewModel;
      desktop.Startup += DesktopOnStartup;
      desktop.MainWindow = mw;
      desktop.MainWindow.DataContext = mw.DataContext;
      WindowHelper.MainWindow = mw;
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
          catch (Exception)
          {
            // ignored
          }
        }
      }
    }
  }
}
