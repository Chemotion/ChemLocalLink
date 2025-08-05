/// <summary>
/// Main view model managing app state, file operations, and UI logic
///
/// Handles chemotion:// URL processing, downloads, and uploads
/// Monitors files for external edits using periodic checksums
/// Manages theme, idle timer, and window state
/// Binds data and commands to MainWindow UI
/// Implements IDisposable for resource cleanup
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using ChemLocalLink.Extensions;
using ChemLocalLink.Helpers;
using ChemLocalLink.Models;
using ChemLocalLink.Services;
using ChemLocalLink.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Timer = System.Timers;

namespace ChemLocalLink.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
  internal readonly HttpClient _httpClient;
  internal string? _filePath;
  internal readonly INotificationService _notificationService;
  internal readonly DispatcherTimer? idleTimer = new DispatcherTimer();
  internal Timer.Timer? fileMonitorTimer;
  internal DateTime lastInteractionTime;
  internal bool isMinimizedByIdleTimer = false;
  internal MainWindowView? mainWindow;
  internal string[]? args;
  internal readonly IDownloadService _downloadService;
  internal readonly IUploadService _uploadService;
  internal readonly IFileService _fileService;
  internal readonly ITokenService _tokenService;
  internal readonly ITrayService _trayService;
  internal readonly IApiHelper _apiHelper;
  internal readonly IWindowHelper _windowHelper;
  internal readonly IProcessHelper _processHelper;
  internal Process? _fileProcess;

  [ObservableProperty]
  private string _appVersion =
    $"{Assembly.GetExecutingAssembly().GetName().Version!.Major}.{Assembly.GetExecutingAssembly().GetName().Version!.Minor}.{Assembly.GetExecutingAssembly().GetName().Version!.Build}";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasFilesDownloaded))]
  ObservableCollection<Downloads> _downloadedFiles = [];

  [ObservableProperty]
  private ObservableCollection<float> _editedFileIds = [];

  [ObservableProperty]
  int _selectedDownloadedFileIndex = -1;

  [ObservableProperty]
  bool _hasFilesDownloaded;

  [ObservableProperty]
  private double _fileUpDownProgress;

  [ObservableProperty]
  string? _fileUpDownProgressText = "";

  [ObservableProperty]
  private string? _url;

  [ObservableProperty]
  string? _status = "";

  [ObservableProperty]
  object? _selectedUrl;

  [ObservableProperty]
  private bool _isAlreadyProcessing;

  [ObservableProperty]
  string _authToken = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ThemeToolTip))]
  private bool _isDarkMode = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;

  [ObservableProperty]
  private string _themeButtonIcon =
    Application.Current!.ActualThemeVariant == ThemeVariant.Dark ? "fa-solid fa-lightbulb" : "fa-regular fa-lightbulb";

  [ObservableProperty]
  private string _themeToolTip =
    Application.Current!.ActualThemeVariant == ThemeVariant.Light ? "Switch to Dark Mode" : "Switch to Light Mode";

  partial void OnIsDarkModeChanged(bool value)
  {
    ThemeButtonIcon = value ? "fa-solid fa-lightbulb" : "fa-regular fa-lightbulb";
    ThemeToolTip = value ? "Switch to Light Mode" : "Switch to Dark Mode";
    Application.Current!.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
    Theme.SaveCurrentTheme(value);
  }

  public MainWindowViewModel(
    HttpClient httpClient,
    IDownloadService downloadService,
    IUploadService uploadService,
    IFileService fileService,
    ITokenService tokenService,
    ITrayService trayService,
    IApiHelper apiHelper,
    IWindowHelper windowHelper,
    INotificationService notificationService,
    IProcessHelper processHelper
  )
  {
    _httpClient = httpClient;
    _downloadService = downloadService;
    _uploadService = uploadService;
    _fileService = fileService;
    _tokenService = tokenService;
    _trayService = trayService;
    _apiHelper = apiHelper;
    _windowHelper = windowHelper;
    _notificationService = notificationService;
    _processHelper = processHelper;

    IsDarkMode = Theme.LoadCurrentTheme();
    Process = new RelayCommand<Task>(_ => Task.Run(async () => await ProcessCommand()));
  }

  public void Initialize(MainWindowView mainWindow, string[] args)
  {
    this.mainWindow = mainWindow;
    this.args = args ?? throw new ArgumentNullException(nameof(args));

    // Set references in the WindowHelper
    _windowHelper.MainWindow = mainWindow;
    _windowHelper.MainWindowViewModel = this;

    SetupEventHandlers();
  }

  private void SetupEventHandlers()
  {
    if (mainWindow != null)
    {
      mainWindow.Loaded += MainWindow_Loaded;
      mainWindow.Deactivated += (s, e) => _windowHelper.Deactivate(this);
    }
  }

  private void MainWindow_Loaded(object? sender, EventArgs e)
  {
    _windowHelper.Load(this);
    fileMonitorTimer = new Timer.Timer(5000); // Check for file changes every 5 seconds instead of 1ms
    fileMonitorTimer.Elapsed += OnTimedEvent;
    fileMonitorTimer.Enabled = true;
  }

  private void OnTimedEvent(object? source, Timer.ElapsedEventArgs e)
  {
    try
    {
      var downloadedFiles = _windowHelper.MainWindowViewModel?.DownloadedFiles;
      if (downloadedFiles == null)
        return;

      foreach (var file in downloadedFiles)
      {
        // Check if file still exists before calculating checksum
        if (!File.Exists(file.FilePath))
        {
          file.IsEdited = false;
          continue;
        }

        var fileSumOnDisk = file.FilePath.FileCheckSum();
        var fileSumOnDownload = file.FileSumOnDownload;

        if (!fileSumOnDisk.Equals(fileSumOnDownload))
        {
          if (
            _windowHelper.MainWindowViewModel != null
            && !_windowHelper.MainWindowViewModel.EditedFileIds.Contains(file.FileId)
          )
          {
            _windowHelper.MainWindowViewModel.EditedFileIds.Add(file.FileId);
          }

          var downloadedFile = DownloadedFiles[DownloadedFiles.IndexOf(file)];
          downloadedFile.IsEdited = !downloadedFile.IsKept;
          downloadedFile.FileSize = new FileInfo(downloadedFile.FilePath).Length.FormatBytes();
        }
        else
        {
          file.IsEdited = false;
        }
      }
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex.Message);
    }
  }

  [RelayCommand]
  public void OnDownloadDoubleTapped(TappedEventArgs e) => OpenFile();

  [RelayCommand]
  public void OpenDownloadDirectory()
  {
    var folderPath = Path.Combine(Path.GetTempPath(), "chemotion");

    if (Directory.Exists(folderPath))
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true, };
        process.Start();
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("xdg-open", folderPath) { UseShellExecute = true, };
        process.Start();
      }
    }
  }

  [RelayCommand]
  public void OpenFile()
  {
    if (DownloadedFiles.Count <= 0)
      return;
    if (SelectedDownloadedFileIndex <= -1)
      return;
    var filePath = DownloadedFiles[SelectedDownloadedFileIndex].FilePath;
    using var process = new Process();
    process.StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true };
    process.Start();
  }

  public RelayCommand<Task> Process;

  public async Task ProcessCommand() => await _processHelper.HandleProcess(this, Url!);

  [RelayCommand]
  public async Task<bool> UploadFiles(string role) => await _uploadService.UploadEditedFiles(this, role);

  [RelayCommand]
  public void DeleteSelectedFile()
  {
    if (SelectedDownloadedFileIndex < 0 || SelectedDownloadedFileIndex >= DownloadedFiles.Count)
      return;

    var selectedFile = DownloadedFiles[SelectedDownloadedFileIndex];
    if (File.Exists(selectedFile.FilePath))
      File.Delete(selectedFile.FilePath);
    DownloadedFiles.RemoveAt(SelectedDownloadedFileIndex);
    var appDataPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "ChemLocalLink"
    );
    Directory.CreateDirectory(appDataPath);
    var jsonFilePath = Path.Combine(appDataPath, "downloads.json");
    if (File.Exists(jsonFilePath) && !string.IsNullOrEmpty(File.ReadAllText(jsonFilePath)))
    {
      var data = JsonConvert.SerializeObject(DownloadedFiles);
      File.WriteAllText(jsonFilePath, data);
    }
    HasFilesDownloaded = DownloadedFiles.Count > 0;
  }

  partial void OnStatusChanged(string? oldValue, string? newValue)
  {
    Task.Run(async () =>
    {
      await Task.Delay(10000);
      Status = "";
    });
  }

  public void Dispose()
  {
    fileMonitorTimer?.Stop();
    fileMonitorTimer?.Dispose();
    idleTimer?.Stop();
    _fileProcess?.Dispose();
  }
}
