/// <summary>
/// Manages system tray icon, context menu, and user interactions
///
/// Adds tray icon with ChemLocalLink branding and tooltip
/// Provides Reload and Exit options in context menu
/// Handles click to restore main window from tray
/// Supports app restart via tray menu
/// Used by WindowHelper during startup
/// </summary>

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using ChemLocalLink.Helpers;
using ChemLocalLink.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace ChemLocalLink.Services;

public interface ITrayService
{
  void InitializeTray(MainWindowViewModel viewModel, IWindowHelper windowHelper);
}

public class TrayService : ITrayService
{
  private TrayIcon? _notifyIcon;
  private MainWindowViewModel? _mainWindowViewModel;

  public TrayService() { }

  public void InitializeTray(MainWindowViewModel viewModel, IWindowHelper windowHelper)
  {
    _mainWindowViewModel = viewModel;

    var _trayMenu = new NativeMenu
    {
      new NativeMenuItem
      {
        Header = "Reload",
        Command = new RelayCommand(() =>
        {
          Dispatcher.UIThread.Post(() =>
          {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
              var process = new System.Diagnostics.Process
              {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                  FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "",
                  UseShellExecute = false,
                },
              };
              process.Start();
              desktop.Shutdown(0);
            }
          });
        }),
      },
      new NativeMenuItemSeparator(),
      new NativeMenuItem
      {
        Header = "Exit",
        Command = new RelayCommand(() =>
        {
          if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
          {
            desktop.Shutdown(0);
          }
        }),
      },
    };

    _notifyIcon = new TrayIcon
    {
      Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ChemLocalLink/Assets/icon.ico"))),
      IsVisible = true,
      ToolTipText = "ChemLocalLink",
      Menu = _trayMenu,
    };

    // wire up events
    _notifyIcon.Clicked += (sender, e) => windowHelper.ShowWindow();
  }
}
