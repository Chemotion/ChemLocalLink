/// <summary>
/// Handles file download, upload, processing, and tracking operations
/// </summary>

using System;
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

  public FileOpsService(
    HttpClient httpClient,
    IApiService apiService,
    INotificationService notificationService,
    IJsonDataService jsonDataService
  )
  {
    _httpClient = httpClient;
    _apiService = apiService;
    _notificationService = notificationService;
    _jsonDataService = jsonDataService;
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
      var fileDir = Path.Combine(Path.GetTempPath(), "chemotion");
      Directory.CreateDirectory(fileDir);

      var filePath = Path.Combine(fileDir, fileName);
      var originalName = Path.GetFileName(filePath);
      var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
      var fileExtension = Path.GetExtension(fileName);
      var counter = 1;

      while (File.Exists(filePath))
      {
        filePath = Path.Combine(fileDir, $"{fileNameWithoutExtension}-{counter}{fileExtension}");
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
          Exp = _apiService.TokenExp(mainWindowView.Url!),
          Origin = originHost,
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
          Exp = _apiService.TokenExp(mainWindowView.Url!),
          Origin = originHost,
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
        foreach (var file in filesToRemove)
        {
          mainWindowViewModel.DownloadedFiles.Remove(file);
          if (File.Exists(file.FilePath))
          {
            File.Delete(file.FilePath);
          }
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
    var upload = await Upload(filePath, mainView, ogIsm);
    if (!upload)
      return false;

    var file = mainView.DownloadedFiles.FirstOrDefault(f => f.FilePath == filePath);
    if (file == null)
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
    File.Delete(file.FilePath);
    mainWindowViewModel.DownloadedFiles.RemoveAt(mainWindowViewModel.SelectedDownloadedFileIndex);
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
    try
    {
      if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
      {
        mainView.Status = NotificationService.Messages.FileAccessError;
        await _notificationService.ShowNotificationAsync(mainView.Status);
        return false;
      }

      byte[] fileContentBytes = await File.ReadAllBytesAsync(filePath);
      var content = new MultipartFormDataContent
      {
        { new ByteArrayContent(fileContentBytes), "file", Path.GetFileName(filePath) },
      };

      var fileName = !string.IsNullOrEmpty(ogIsm) ? ogIsm : new FileInfo(filePath).Name;
      content.Add(new StringContent(fileName), "attachmentName");

      var fileSize = new FileInfo(filePath).Length.FormatBytes();
      var progress = new Progress<ProgressModel>(prog =>
      {
        var currentProgress = prog.BytesRead > new FileInfo(filePath).Length ? fileSize : prog.BytesRead.FormatBytes();
        mainView.FileUpDownProgressText = $"Uploaded {currentProgress} out of {fileSize}.";
        mainView.Status = mainView.FileUpDownProgressText;
        mainView.FileUpDownProgress = prog.Percentage;
        if (prog.BytesRead >= new FileInfo(filePath).Length)
        {
          mainView.Status = NotificationService.Messages.UploadSuccessful;
        }
      });

      var response = await _httpClient.PostWithProgressAsync(
        _apiService.UploadUrl(mainView.AuthToken),
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

  #endregion
}
