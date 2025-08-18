/// <summary>
/// Handles file download, upload, processing, and tracking operations
/// </summary>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using ChemLocalLink.Models;
using ChemLocalLink.Utilities;
using ChemLocalLink.ViewModels;
using MsBox.Avalonia;

namespace ChemLocalLink.Services;

public interface IFileOpsService
{
  Task<(string filePath, string originalName)?> DownloadFile(MainWindowViewModel mainWindowView, string token);
  Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName);
  Task<bool> UploadEditedFiles(MainWindowViewModel mainWindowView, string role = "");
}

internal class FileOpsService : IFileOpsService
{
  private readonly HttpClient _httpClient;
  private readonly IApiService _apiService;
  private readonly INotificationService _notificationService;
  private readonly IJsonDataService _jsonDataService;
  private readonly IPathService _pathService;

  public FileOpsService(
    HttpClient httpClient,
    IApiService apiService,
    INotificationService notificationService,
    IJsonDataService jsonDataService,
    IPathService pathService
  )
  {
    _httpClient = httpClient;
    _apiService = apiService;
    _notificationService = notificationService;
    _jsonDataService = jsonDataService;
    _pathService = pathService;
  }

  #region Download Operations

  public async Task<(string filePath, string originalName)?> DownloadFile(
    MainWindowViewModel mainWindowView,
    string authToken
  )
  {
    try
    {
      var token = authToken?.Length < 1 ? mainWindowView?.Url?[(mainWindowView.Url.LastIndexOf('/') + 1)..] : authToken;

      if (token == null || mainWindowView == null)
      {
        return null;
      }

      mainWindowView.AuthToken = token;
      if (mainWindowView.Url != null)
        _apiService.SetFromUrl(mainWindowView.Url);
      var downloadUrl = _apiService.DownloadUrl(token);
      mainWindowView.Status = NotificationService.Messages.Downloading;
      var progress = new Progress<ProgressModel>(progressInfo =>
      {
        if (mainWindowView != null)
          mainWindowView.Status =
            $"Downloaded {progressInfo.BytesRead.FormatBytes()} out of {progressInfo.TotalBytesExpected?.FormatBytes() ?? "0"}.";
      });

      var (response, fileContentBytes) = await _httpClient.GetWithProgressAsync(downloadUrl, progress);
      var headers = response.Content.Headers;

      var _headers = headers.ToImmutableDictionary();
      var contentDisposition = _headers["Content-Disposition"].FirstOrDefault();

      if (!response.IsSuccessStatusCode || contentDisposition == null || !contentDisposition.Contains("filename"))
      {
        if (mainWindowView != null)
          mainWindowView.Status = NotificationService.Messages.DownloadFail;
        await _notificationService.ShowNotificationAsync(
          mainWindowView?.Status ?? NotificationService.Messages.DownloadFail
        );

        return null;
      }

      var fileName = contentDisposition[(contentDisposition.IndexOf("=", StringComparison.Ordinal) + 1)..];
      var baseDir = _pathService.GetDownloadDirectory();
      Directory.CreateDirectory(baseDir);

      var originHost = mainWindowView.Url.ExtractOriginHost() ?? "Unknown";
      var originDir = Path.Combine(baseDir, originHost);

      string fileDir;
      if (!string.IsNullOrEmpty(mainWindowView.DeepLinkPath))
      {
        var pathSegments = mainWindowView.DeepLinkPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        fileDir = originDir;
        foreach (var segment in pathSegments)
        {
          fileDir = Path.Combine(fileDir, segment);
        }
      }
      else
      {
        fileDir = originDir;
      }

      Directory.CreateDirectory(fileDir);

      var filePath = Path.Combine(fileDir, fileName).NormalizePath();
      var originalName = Path.GetFileName(filePath);
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
      var fileExtension = Path.GetExtension(fileName);
      var counter = 1;

      while (File.Exists(filePath))
      {
        filePath = Path.Combine(fileDir, $"{fileNameWithoutExtension}-{counter}{fileExtension}").NormalizePath();
        counter++;
      }

      await using var fileStream = new FileStream(
        filePath,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        4096,
        true
      );
      if (fileContentBytes != null)
        await fileStream.WriteAsync(fileContentBytes.AsMemory(0, fileContentBytes.Length));
      return (filePath, originalName);
    }
    catch (Exception ex)
    {
      var errorMessage =
        ex is IOException ? NotificationService.Messages.FileAccessError : NotificationService.Messages.UnExpectedError;
      if (mainWindowView != null)
        mainWindowView.Status = errorMessage;
      await _notificationService.ShowNotificationAsync(mainWindowView?.Status ?? errorMessage);
      return null;
    }
  }

  #endregion

  #region File Processing

  public Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName)
  {
    try
    {
      if (filePath == null)
        return Task.CompletedTask;
      mainWindowView?._fileProcess?.Dispose();

      if (mainWindowView != null)
      {
        mainWindowView._fileProcess = new Process
        {
          StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true },
        };
      }
      else
      {
        throw new InvalidOperationException("MainWindowViewModel is not initialized.");
      }

      mainWindowView._fileProcess.Start();

      var random = new Random(2345);

      {
        var id =
          mainWindowView.DownloadedFiles?.Any() ?? false
            ? mainWindowView.DownloadedFiles.Max(f => f.FileId) + 1
            : random.NextInt64(10000, 999999);

        // extract origin from URL
        var originHost = mainWindowView.Url.ExtractOriginHost() ?? string.Empty;
        var token = mainWindowView.AuthToken; // store per-file token
        var exp = _apiService.TokenExp(mainWindowView.Url!);

        var download = new DownloadModel()
        {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = exp,
          Origin = originHost,
          Path = mainWindowView.DeepLinkPath,
          Token = token,
          SourceUrl = mainWindowView.Url
        };

        File.SetCreationTime(filePath, DateTime.Now);

        var appDataPath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          "ChemLocalLink"
        );
        Directory.CreateDirectory(appDataPath);
        var jsonFilePath = Path.Combine(appDataPath, "downloads.json");

        var jsonObject = new
        {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = exp,
          Origin = originHost,
          Path = mainWindowView.DeepLinkPath,
          Token = token,
          SourceUrl = mainWindowView.Url
        };

        _jsonDataService.AppendJsonToFile(jsonFilePath, jsonObject);

        Dispatcher.UIThread.Post(() =>
        {
          mainWindowView.DownloadedFiles?.Insert(0, download);
          mainWindowView.HasFilesDownloaded = (mainWindowView.DownloadedFiles?.Count ?? 0) > 0;
          mainWindowView.RebuildGroups();
        });
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error processing file: {ex.Message}");
    }

    return Task.CompletedTask;
  }

  #endregion

  #region Upload Operations

  public async Task<bool> UploadEditedFiles(MainWindowViewModel mainWindowView, string role = "")
  {
    try
    {
      if (mainWindowView?.DownloadedFiles.Count == 0)
      {
        mainWindowView!.Status = NotificationService.Messages.NoDownloads;
        await _notificationService.ShowNotificationAsync(mainWindowView.Status);
        return false;
      }

      if (mainWindowView?.SelectedDownloadedFileIndex > -1)
      {
        return await HandleSingleFileUpload(role, mainWindowView);
      }
      else
      {
        return await HandleMultipleFilesUpload(role, mainWindowView!);
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error uploading files: {ex.Message}");
      return false;
    }
    finally
    {
      if (mainWindowView?.DownloadedFiles != null)
      {
        mainWindowView.HasFilesDownloaded = mainWindowView.DownloadedFiles.Count > 0;
      }
    }
  }

  private async Task<bool> HandleSingleFileUpload(string role, MainWindowViewModel mainWindowViewModel)
  {
    var file = mainWindowViewModel.DownloadedFiles[mainWindowViewModel.SelectedDownloadedFileIndex];

    if (file.IsKept)
    {
      mainWindowViewModel.Status = NotificationService.Messages.FileKept;
      await _notificationService.ShowNotificationAsync(mainWindowViewModel.Status);
      return false;
    }

    var fileSumOnDisk = file.FilePath.FileCheckSum();
    var fileSumOnDownload = file.FileSumOnDownload;

    if (fileSumOnDisk.Equals(fileSumOnDownload))
    {
      mainWindowViewModel.Status = NotificationService.Messages.FileNotEdited;
      await _notificationService.ShowNotificationAsync(mainWindowViewModel.Status);
      return false;
    }

    return await AttemptUpload(file.FilePath, mainWindowViewModel, file.OriginalFileName, role);
  }

  private async Task<bool> HandleMultipleFilesUpload(string role, MainWindowViewModel mainWindowViewModel)
  {
    var tempList = mainWindowViewModel.DownloadedFiles.ToList();
    var filesToRemove = new ObservableCollection<DownloadModel>();

    foreach (var file in tempList)
    {
      if (file.IsKept)
      {
        mainWindowViewModel.Status = NotificationService.Messages.FileKept;
        await _notificationService.ShowNotificationAsync(mainWindowViewModel.Status);
        continue;
      }

      var fileSumOnDisk = file.FilePath.FileCheckSum();
      var fileSumOnDownload = file.FileSumOnDownload;

      if (!fileSumOnDisk.Equals(fileSumOnDownload))
      {
        var upload = await AttemptUpload(file.FilePath, mainWindowViewModel, file.OriginalFileName, role);
        if (upload)
        {
          filesToRemove.Add(file);
        }
      }
    }

    if (filesToRemove.Count == 0)
    {
      mainWindowViewModel.Status = NotificationService.Messages.FileNotEdited;
      await _notificationService.ShowNotificationAsync(mainWindowViewModel.Status);
      return false;
    }

    return await HandleFileRemoval(filesToRemove, role, mainWindowViewModel);
  }

  private async Task<bool> HandleFileRemoval(
    ObservableCollection<DownloadModel> filesToRemove,
    string role,
    MainWindowViewModel mainWindowViewModel
  )
  {
    try
    {
      if (role == "delete")
      {
        var directoriesToCleanup = new List<string>();

        foreach (var file in filesToRemove)
        {
          mainWindowViewModel.DownloadedFiles.Remove(file);
          if (File.Exists(file.FilePath))
          {
            var fileDirectory = Path.GetDirectoryName(file.FilePath);
            if (fileDirectory != null)
            {
              directoriesToCleanup.Add(fileDirectory);
            }
            File.Delete(file.FilePath);
          }
        }

        foreach (var directory in directoriesToCleanup.Distinct())
        {
          CleanupEmptyDirectories(directory, mainWindowViewModel);
        }
      }
      else
      {
        foreach (var file in filesToRemove)
        {
          var fileToKeep = mainWindowViewModel.DownloadedFiles[mainWindowViewModel.DownloadedFiles.IndexOf(file)];
          fileToKeep.IsEdited = false;
          fileToKeep.IsKept = true;
        }
      }

      await _jsonDataService.WriteDataToAppData(mainWindowViewModel);
      return true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error handling multiple files upload: {ex.Message}");
      await MessageBoxManager.GetMessageBoxStandard("Error", "Unexpected error occurred").ShowAsync();
      return false;
    }
  }

  private async Task<bool> AttemptUpload(string filePath, MainWindowViewModel mainView, string ogIsm, string role)
  {
    var file = mainView.DownloadedFiles.FirstOrDefault(f => f.FilePath == filePath);
    if (file == null)
      return false;

    var upload = await Upload(file, mainView, ogIsm);
    if (!upload)
      return false;

    if (role == "delete")
    {
      return DeleteFile(file, mainView);
    }
    else
    {
      return KeepFile(file, mainView);
    }
  }

  private bool DeleteFile(DownloadModel file, MainWindowViewModel mainWindowViewModel)
  {
    string? fileDirectory = null;
    try
    {
      if (File.Exists(file.FilePath))
      {
        fileDirectory = Path.GetDirectoryName(file.FilePath);
        File.Delete(file.FilePath);
      }
    }
    catch { }

    mainWindowViewModel.DownloadedFiles.RemoveAt(mainWindowViewModel.SelectedDownloadedFileIndex);

    if (fileDirectory != null)
    {
      CleanupEmptyDirectories(fileDirectory, mainWindowViewModel);
    }

    _jsonDataService.WriteDataToAppData(mainWindowViewModel);
    mainWindowViewModel.RebuildGroups();
    return true;
  }

  private bool KeepFile(DownloadModel file, MainWindowViewModel mainWindowViewModel)
  {
    file.IsEdited = false;
    file.IsKept = true;
    _jsonDataService.WriteDataToAppData(mainWindowViewModel);
    return true;
  }

  public async Task<bool> Upload(string filePath, MainWindowViewModel mainView, string ogIsm = "")
  {
    // legacy path kept for backward compatibility
    var model = mainView.DownloadedFiles.FirstOrDefault(f => f.FilePath == filePath);
    if (model == null)
      return false;
    return await Upload(model, mainView, ogIsm);
  }

  private async Task<bool> Upload(DownloadModel fileModel, MainWindowViewModel mainView, string ogIsm = "")
  {
    try
    {
      var token = fileModel.Token ?? mainView.AuthToken;
      if (string.IsNullOrWhiteSpace(token))
      {
        mainView.Status = NotificationService.Messages.UploadFail;
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return false;
      }

      // expiration check
      var expUtc = DateTimeOffset.FromUnixTimeSeconds(fileModel.Exp).UtcDateTime;
      if (DateTime.UtcNow > expUtc)
      {
        mainView.Status = NotificationService.Messages.UploadFail; // expired
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return false;
      }

      var normalizedPath = fileModel.FilePath.NormalizePath();
      if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
      {
        mainView.Status = NotificationService.Messages.FileAccessError;
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return false;
      }

      byte[] fileContentBytes = await File.ReadAllBytesAsync(normalizedPath);
      var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(fileContentBytes), "file", Path.GetFileName(normalizedPath) },
      };

      var fileName = Path.GetFileName(normalizedPath);
      content.Add(new StringContent(fileName), "attachmentName");

      var fileSize = new FileInfo(normalizedPath).Length.FormatBytes();
      var progress = new Progress<ProgressModel>(prog =>
      {
        var currentProgress =
          prog.BytesRead > new FileInfo(normalizedPath).Length ? fileSize : prog.BytesRead.FormatBytes();
        mainView.FileUpDownProgressText = $"Uploaded {currentProgress} out of {fileSize}.";
        mainView.Status = mainView.FileUpDownProgressText;
        mainView.FileUpDownProgress = prog.Percentage;
        if (prog.BytesRead >= new FileInfo(normalizedPath).Length)
        {
          mainView.Status = NotificationService.Messages.UploadSuccessful;
        }
      });

      if (!string.IsNullOrWhiteSpace(fileModel.SourceUrl))
      {
        _apiService.SetFromUrl(fileModel.SourceUrl);
      }

      var response = await _httpClient.PostWithProgressAsync(
        _apiService.UploadUrl(token),
        content,
        progress,
        isUpload: true
      );

      if (response.IsSuccessStatusCode)
      {
        mainView.Status = NotificationService.Messages.UploadSuccessful;
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return true;
      }
      else
      {
        mainView.Status = NotificationService.Messages.UploadFail;
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return false;
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error uploading file: {ex.Message}");
      mainView.Status = NotificationService.Messages.UploadFail;
      await _notificationService.ShowNotificationAsync(mainView.Status);
      return false;
    }
  }

  private void CleanupEmptyDirectories(string startDirectory, MainWindowViewModel mainWindowViewModel)
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

  #endregion
}
