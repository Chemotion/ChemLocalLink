/// <summary>
/// Manages window state, startup, and user interaction tracking
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ChemLocalLink.Models;
using ChemLocalLink.Utilities;
using ChemLocalLink.ViewModels;
using ChemLocalLink.Views;
using Newtonsoft.Json;

namespace ChemLocalLink.Services;

public interface IWindowService
{
  MainWindowViewModel? MainWindowViewModel { get; set; }
  MainWindowView? MainWindow { get; set; }
  void Deactivate(MainWindowViewModel mainWindowView);
  void Load(MainWindowViewModel mainWindowView);
  void ShowWindow();
}

public class WindowService : IWindowService
{
  private readonly ITrayService _trayService;
  private readonly INotificationService _notificationService;
  private readonly IWorkflowService _workflowService;
  private readonly IPathService _pathService;

  public MainWindowViewModel? MainWindowViewModel { get; set; }
  public MainWindowView? MainWindow { get; set; }

  public WindowService(
    ITrayService trayService,
    INotificationService notificationService,
    IWorkflowService workflowService,
    IPathService pathService
  )
  {
    _trayService = trayService;
    _notificationService = notificationService;
    _workflowService = workflowService;
    _pathService = pathService;
  }

  public void Deactivate(MainWindowViewModel mainWindowView)
  {
    var minimized = mainWindowView is { isMinimizedByIdleTimer: false, mainWindow.WindowState: WindowState.Minimized };
    if (mainWindowView.mainWindow != null)
      mainWindowView.mainWindow.ShowInTaskbar = !minimized;
    mainWindowView.isMinimizedByIdleTimer = minimized;
    if (mainWindowView.idleTimer == null)
      return;
    mainWindowView.idleTimer.IsEnabled = !minimized;
    if (minimized)
      mainWindowView.idleTimer.Stop();
    else
      mainWindowView.idleTimer.Start();
  }

  public void Load(MainWindowViewModel mainWindowView)
  {
    _trayService.InitializeTray(mainWindowView, this);

    Task.Run(async () =>
    {
      try
      {
        var appDataPath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          "ChemLocalLink"
        );
        Directory.CreateDirectory(appDataPath);
        var filePath = Path.Combine(appDataPath, "downloads.json");

        if (File.Exists(filePath) && !string.IsNullOrEmpty(File.ReadAllText(filePath)))
        {
          var data = File.ReadAllText(filePath);
          var downloads = JsonConvert.DeserializeObject<ObservableCollection<DownloadModel>>(data) ?? new();

          // migrate legacy temp directory files if needed
          _pathService.MigrateFromLegacyTempDir(downloads.ToList());
          // persist any FilePath changes
          var migratedData = JsonConvert.SerializeObject(downloads, Formatting.Indented);
          File.WriteAllText(filePath, migratedData);

          if (downloads.Count == 0)
          {
            mainWindowView.HasFilesDownloaded = false;
          }
          else
          {
            foreach (var download in downloads)
            {
              if (File.Exists(download.FilePath))
              {
                if (download.Path == null)
                  download.Path = string.Empty;
                if (download.Token == null && !string.IsNullOrWhiteSpace(mainWindowView.Url))
                  download.Token = download.FilePath.ExtractAuthToken();

                if (download.IsKept && download.IsEdited)
                  download.IsEdited = false;

                if (string.IsNullOrWhiteSpace(download.Origin) && !string.IsNullOrWhiteSpace(mainWindowView.Url))
                {
                  try
                  {
                    var uri = new Uri(mainWindowView.Url);
                    download.Origin = uri.Host;
                  }
                  catch { }
                }

                // ensure new fields exist for legacy entries
                if (download.SourceUrl == null && !string.IsNullOrWhiteSpace(mainWindowView.Url))
                  download.SourceUrl = mainWindowView.Url;
                if (download.FileDownloadTimeStamp == default)
                  download.FileDownloadTimeStamp = File.GetLastWriteTime(download.FilePath);

                mainWindowView.DownloadedFiles.Insert(0, download);
              }
            }

            var newData = JsonConvert.SerializeObject(mainWindowView.DownloadedFiles.Reverse(), Formatting.Indented);
            File.WriteAllText(filePath, newData);
            mainWindowView.HasFilesDownloaded = mainWindowView.DownloadedFiles.Count > 0;

            mainWindowView.RebuildGroups();
          }
        }
        else
        {
          mainWindowView.HasFilesDownloaded = false;
        }

        if (mainWindowView.args?.Length > 0)
        {
          var originalUrl = mainWindowView.args.First();

          try
          {
            var uri = new Uri(originalUrl);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            string? urlParam = queryParams.Get("url");
            string? pathParam = queryParams.Get("path");

            if (string.IsNullOrEmpty(urlParam))
            {
              mainWindowView.Status = NotificationService.Messages.InvalidUrl;
              await _notificationService.ShowNotificationAsync(mainWindowView.Status);
              return;
            }

            var parsedUrl = HttpUtility.UrlDecode(urlParam);

            if (!string.IsNullOrEmpty(pathParam))
            {
              mainWindowView.DeepLinkPath = HttpUtility.UrlDecode(pathParam);
            }

            if (parsedUrl == null || parsedUrl == "invalid uri")
            {
              mainWindowView.Status = NotificationService.Messages.InvalidUrl;
              await _notificationService.ShowNotificationAsync(mainWindowView.Status);
              return;
            }

            var authToken = parsedUrl.ExtractAuthToken();
            if (authToken == null)
            {
              mainWindowView.Status = NotificationService.Messages.TokenFail;
              await _notificationService.ShowNotificationAsync(mainWindowView.Status);
              return;
            }

            mainWindowView.Url = parsedUrl;
            await _workflowService.HandleProcess(mainWindowView, parsedUrl);
          }
          catch (Exception)
          {
            mainWindowView.Status = NotificationService.Messages.InvalidUrl;
            await _notificationService.ShowNotificationAsync(mainWindowView.Status);
            return;
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Exception in Load method: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
      }
    });
    MinimizeWindowOnIdle();
  }

  private void MinimizeWindowOnIdle()
  {
    try
    {
      var window = MainWindow!;

      var idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(300) };
      idleTimer.Tick += async (sender, e) =>
      {
        var elapsedTime = DateTime.Now - MainWindowViewModel!.lastInteractionTime;

        if (!(elapsedTime.TotalSeconds > 300))
          return;
        MainWindowViewModel.isMinimizedByIdleTimer = true;
        window.WindowState = WindowState.Minimized;
        idleTimer.IsEnabled = false;
        idleTimer.Stop();
        await _notificationService.ShowNotificationAsync(NotificationService.Messages.Minimize);
        idleTimer.Start();
        window.PointerPressed += (sender, eventArgs) => ResetLastInteractionTime(MainWindowViewModel);
        window.PointerMoved += (sender, eventArgs) => ResetLastInteractionTime(MainWindowViewModel);
        window.KeyDown += (sender, eventArgs) => ResetLastInteractionTime(MainWindowViewModel);
        MainWindowViewModel.lastInteractionTime = DateTime.Now;
      };
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in MinimizeWindowOnIdle: {ex.Message}");
      throw;
    }
  }

  private void ResetLastInteractionTime(MainWindowViewModel mainWindowView)
  {
    mainWindowView.lastInteractionTime = DateTime.Now;

    if (!mainWindowView.isMinimizedByIdleTimer)
    {
      return;
    }

    if (mainWindowView.mainWindow != null)
    {
      mainWindowView.mainWindow.WindowState = WindowState.Normal;
      mainWindowView.mainWindow.ShowInTaskbar = true;
    }
    mainWindowView.isMinimizedByIdleTimer = false;

    if (mainWindowView.idleTimer != null)
    {
      mainWindowView.idleTimer.IsEnabled = true;
      mainWindowView.idleTimer.Start();
    }
    else
    {
      Console.WriteLine("Error: idleTimer is null.");
    }
  }

  public void ShowWindow()
  {
    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopApp)
      return;
    var mainWindow = desktopApp.MainWindow;
    if (mainWindow == null)
      return;
    mainWindow.Show();
    mainWindow.WindowState = WindowState.Normal;
    mainWindow.ShowInTaskbar = true;
  }
}
