/// <summary>
/// Main view model managing app state, file operations, and UI logic
/// </summary>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using ChemLocalLink.Models;
using ChemLocalLink.Services;
using ChemLocalLink.Utilities;
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
  internal readonly IFileOpsService _fileOpsService;
  internal readonly IApiService _apiService;
  internal readonly ITrayService _trayService;
  internal readonly IWindowService _windowService;
  internal readonly IWorkflowService _workflowService;
  internal readonly IJsonDataService _jsonDataService;
  internal readonly IPathService _pathService;
  internal readonly ISessionService _sessionService;
  internal Process? _fileProcess;

  [ObservableProperty]
  private string _appVersion =
    $"{Assembly.GetExecutingAssembly().GetName().Version!.Major}.{Assembly.GetExecutingAssembly().GetName().Version!.Minor}.{Assembly.GetExecutingAssembly().GetName().Version!.Build}";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasFilesDownloaded))]
  ObservableCollection<DownloadModel> _downloadedFiles = [];

  [ObservableProperty]
  private ObservableCollection<OriginGroupModel> _downloadedByOrigin = [];

  [ObservableProperty]
  private DownloadModel? _selectedDownloadedFile;

  partial void OnSelectedDownloadedFileChanged(DownloadModel? value)
  {
    if (value == null)
    {
      SelectedDownloadedFileIndex = -1;
      return;
    }

    var idx = DownloadedFiles.IndexOf(value);
    SelectedDownloadedFileIndex = idx;
  }

  [ObservableProperty]
  private ObservableCollection<float> _editedFileIds = [];

  [ObservableProperty]
  int _selectedDownloadedFileIndex = -1;

  partial void OnSelectedDownloadedFileIndexChanged(int value)
  {
    if (value >= 0 && value < DownloadedFiles.Count)
    {
      if (!ReferenceEquals(SelectedDownloadedFile, DownloadedFiles[value]))
      {
        SelectedDownloadedFile = DownloadedFiles[value];
      }
    }
  }

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
  string? _deepLinkPath;

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
    _jsonDataService.SaveCurrentTheme(value);
  }

  public MainWindowViewModel(
    HttpClient httpClient,
    IFileOpsService fileOpsService,
    IApiService apiService,
    ITrayService trayService,
    IWindowService windowService,
    INotificationService notificationService,
    IWorkflowService workflowService,
    IJsonDataService jsonDataService,
    IPathService pathService,
    ISessionService sessionService
  )
  {
    _httpClient = httpClient;
    _fileOpsService = fileOpsService;
    _apiService = apiService;
    _trayService = trayService;
    _windowService = windowService;
    _notificationService = notificationService;
    _workflowService = workflowService;
    _jsonDataService = jsonDataService;
    _pathService = pathService;
    _sessionService = sessionService;

    IsDarkMode = _jsonDataService.LoadCurrentTheme();
    Process = new RelayCommand<Task>(_ => Task.Run(async () => await ProcessCommand()));
  }

  public void Initialize(MainWindowView mainWindow, string[] args)
  {
    this.mainWindow = mainWindow;
    this.args = args ?? throw new ArgumentNullException(nameof(args));

    // Set references in the WindowService
    _windowService.MainWindow = mainWindow;
    _windowService.MainWindowViewModel = this;

    SetupEventHandlers();

    DownloadedFiles.CollectionChanged += (s, e) => RebuildGroups();
  }

  private void SetupEventHandlers()
  {
    if (mainWindow != null)
    {
      mainWindow.Loaded += MainWindow_Loaded;
      mainWindow.Deactivated += (s, e) => _windowService.Deactivate(this);
    }
  }

  private void MainWindow_Loaded(object? sender, EventArgs e)
  {
    _windowService.Load(this);
    fileMonitorTimer = new Timer.Timer(5000);
    fileMonitorTimer.Elapsed += OnTimedEvent;
    fileMonitorTimer.Enabled = true;
  }

  private void OnTimedEvent(object? source, Timer.ElapsedEventArgs e)
  {
    try
    {
      var downloadedFiles = _windowService.MainWindowViewModel?.DownloadedFiles;
      if (downloadedFiles == null)
        return;

      foreach (var file in downloadedFiles)
      {
        // check if file still exists before calculating checksum
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
            _windowService.MainWindowViewModel != null
            && !_windowService.MainWindowViewModel.EditedFileIds.Contains(file.FileId)
          )
          {
            _windowService.MainWindowViewModel.EditedFileIds.Add(file.FileId);
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
    var folderPath =
      SelectedDownloadedFile != null
        ? Path.GetDirectoryName(SelectedDownloadedFile.FilePath) ?? _pathService.GetDownloadDirectory()
        : _pathService.GetDownloadDirectory();

    Debug.WriteLine($"Opening folder: {folderPath}");

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
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("open", folderPath) { UseShellExecute = true, };
        process.Start();
      }
    }
  }

  [RelayCommand]
  public void OpenFile()
  {
    if (DownloadedFiles.Count <= 0)
      return;

    var fileModel =
      SelectedDownloadedFileIndex > -1 ? DownloadedFiles[SelectedDownloadedFileIndex] : SelectedDownloadedFile;

    if (fileModel == null)
      return;

    var filePath = fileModel.FilePath;
    using var process = new Process();
    process.StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true };
    process.Start();
  }

  public RelayCommand<Task> Process;

  public async Task ProcessCommand() => await _workflowService.HandleProcess(this, Url!);

  [RelayCommand]
  public async Task<bool> UploadFiles(string role) => await _fileOpsService.UploadEditedFiles(this, role);

  [RelayCommand]
  public void DeleteSelectedFile()
  {
    DownloadModel? selectedFile = null;

    if (SelectedDownloadedFileIndex >= 0 && SelectedDownloadedFileIndex < DownloadedFiles.Count)
    {
      selectedFile = DownloadedFiles[SelectedDownloadedFileIndex];
    }
    else if (SelectedDownloadedFile != null)
    {
      selectedFile = SelectedDownloadedFile;
    }

    if (selectedFile == null)
      return;

    string? fileDirectory = null;
    try
    {
      if (File.Exists(selectedFile.FilePath))
      {
        fileDirectory = Path.GetDirectoryName(selectedFile.FilePath);
        File.Delete(selectedFile.FilePath);
      }
      var backup = selectedFile.FilePath + "~";
      if (File.Exists(backup))
        File.Delete(backup);
    }
    catch { }

    DownloadedFiles.Remove(selectedFile);

    if (fileDirectory != null)
    {
      CleanupEmptyDirectories(fileDirectory);
    }

    var appDataPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "ChemLocalLink"
    );
    Directory.CreateDirectory(appDataPath);
    var jsonFilePath = Path.Combine(appDataPath, "downloads.json");
    if (File.Exists(jsonFilePath) && !string.IsNullOrEmpty(File.ReadAllText(jsonFilePath)))
    {
      var data = JsonConvert.SerializeObject(DownloadedFiles, Formatting.Indented);
      File.WriteAllText(jsonFilePath, data);
    }
    HasFilesDownloaded = DownloadedFiles.Count > 0;

    RebuildGroups();
  }

  [RelayCommand]
  public async Task<bool> ExportSession(string targetPath)
  {
    return await _sessionService.ExportSessionAsync(this, targetPath);
  }

  [RelayCommand]
  public async Task<bool> ImportSession(string sourcePath)
  {
    return await _sessionService.ImportSessionAsync(this, sourcePath);
  }

  [RelayCommand]
  public void SelectFile(DownloadModel file)
  {
    SelectedDownloadedFile = file;
  }

  [RelayCommand]
  public async Task DuplicateAndRenameFile()
  {
    if (SelectedDownloadedFile == null)
      return;

    var originalName = Path.GetFileNameWithoutExtension(SelectedDownloadedFile.FileName);
    var extension = Path.GetExtension(SelectedDownloadedFile.FileName);
    var suggestedName = $"{originalName}_copy{extension}";

    var result = await _fileOpsService.DuplicateAndRenameFile(this, SelectedDownloadedFile, suggestedName);

    if (result != null)
    {
      Status = $"File duplicated successfully as '{result.FileName}'";
    }
  }

  [RelayCommand]
  public async Task ScanFolderForNewFiles()
  {
    await _fileOpsService.ScanFolderForNewFiles(this);
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

  partial void OnDownloadedFilesChanged(ObservableCollection<DownloadModel> value)
  {
    RebuildGroups();
  }

  private int _rebuildVersion = 0;

  public async void RebuildGroups()
  {
    try
    {
      var currentVersion = ++_rebuildVersion;

      // small debounce for rapid updates
      await Task.Delay(10);
      if (currentVersion != _rebuildVersion)
        return;

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        var groups = DownloadedFiles
          .GroupBy(d => (d.Origin ?? string.Empty).Trim().ToLowerInvariant())
          .Select(g =>
          {
            var originName = string.IsNullOrWhiteSpace(g.Key)
              ? "Unknown"
              : g.Select(x => x.Origin).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o)) ?? g.Key;

            var originGroup = new OriginGroupModel { Origin = originName };

            var filesByPath = g.GroupBy(f => f.Path ?? string.Empty).ToList();

            var filesWithoutPaths =
              filesByPath.FirstOrDefault(fp => string.IsNullOrEmpty(fp.Key))?.ToList() ?? new List<DownloadModel>();
            foreach (var file in filesWithoutPaths.OrderByDescending(f => f.FileDownloadTimeStamp))
            {
              originGroup.Files.Add(file);
            }

            var filesWithPaths = filesByPath.Where(fp => !string.IsNullOrEmpty(fp.Key)).ToList();
            if (filesWithPaths.Any())
            {
              BuildFolderHierarchy(originGroup, filesWithPaths);
            }

            return originGroup;
          })
          .OrderBy(g => g.Origin)
          .ToList();

        DownloadedByOrigin.Clear();
        foreach (var g in groups)
        {
          DownloadedByOrigin.Add(g);
        }
      });
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex.Message);
    }
  }

  private void BuildFolderHierarchy(OriginGroupModel originGroup, List<IGrouping<string, DownloadModel>> filesWithPaths)
  {
    foreach (var pathGroup in filesWithPaths)
    {
      var fullPath = pathGroup.Key;
      var files = pathGroup.OrderByDescending(f => f.FileDownloadTimeStamp).ToList();

      var collapsedFolder = new FolderModel
      {
        Name = fullPath,
        FullPath = fullPath,
        IsCollapsed = false
      };
      foreach (var f in files)
      {
        collapsedFolder.Files.Add(f);
      }

      originGroup.Folders.Add(collapsedFolder);
    }
  }

  partial void OnHasFilesDownloadedChanged(bool value)
  {
    RebuildGroups();
  }

  [RelayCommand]
  public async Task ClearAllDownloads()
  {
    try
    {
      var directoriesToCheck = new HashSet<string>();

      foreach (var d in DownloadedFiles.ToList())
      {
        try
        {
          if (File.Exists(d.FilePath))
          {
            var fileDirectory = Path.GetDirectoryName(d.FilePath);
            if (fileDirectory != null)
            {
              directoriesToCheck.Add(fileDirectory);
            }
            File.Delete(d.FilePath);
          }

          var backup = d.FilePath + "~";
          if (File.Exists(backup))
            File.Delete(backup);
        }
        catch { }
      }
      DownloadedFiles.Clear();
      HasFilesDownloaded = false;
      await _jsonDataService.WriteDataToAppData(this);

      // Clean up empty directories
      foreach (var directory in directoriesToCheck)
      {
        CleanupEmptyDirectories(directory);
      }

      // purge any stray backup files
      try
      {
        var dir = _pathService.GetDownloadDirectory();
        if (Directory.Exists(dir))
        {
          foreach (var orphan in Directory.EnumerateFiles(dir, "*~", SearchOption.TopDirectoryOnly))
          {
            try
            {
              File.Delete(orphan);
            }
            catch { }
          }
        }
      }
      catch { }

      Status = "All downloads cleared";
    }
    catch
    {
      Status = "Clear failed";
    }
  }

  private void CleanupEmptyDirectories(string startDirectory)
  {
    try
    {
      var downloadRoot = _pathService.GetDownloadDirectory();
      var currentDir = startDirectory;

      while (
        !string.IsNullOrEmpty(currentDir)
        && currentDir.Length > downloadRoot.Length
        && currentDir.StartsWith(downloadRoot, StringComparison.OrdinalIgnoreCase)
      )
      {
        try
        {
          if (Directory.Exists(currentDir))
          {
            var files = Directory.GetFiles(currentDir);
            var subdirs = Directory.GetDirectories(currentDir);

            if (files.Length == 0 && subdirs.Length == 0)
            {
              Directory.Delete(currentDir);
              currentDir = Path.GetDirectoryName(currentDir);
            }
            else
            {
              break;
            }
          }
          else
          {
            currentDir = Path.GetDirectoryName(currentDir);
          }
        }
        catch
        {
          break;
        }
      }
    }
    catch { }
  }
}
