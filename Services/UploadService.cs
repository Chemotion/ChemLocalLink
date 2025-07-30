/// <summary>
/// Uploads edited files to Chemotion servers with progress and file validation
///
/// Supports "delete" and "keep" upload modes
/// Validates edits via checksum before uploading
/// Handles single and batch uploads with progress reporting
/// Updates local file state and downloads.json after upload
/// Notifies users of success or failure
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ChemLocalLink.Extensions;
using ChemLocalLink.Helpers;
using ChemLocalLink.Models;
using ChemLocalLink.ViewModels;
using MsBox.Avalonia;

namespace ChemLocalLink.Services;

public interface IUploadService
{
  Task<bool> UploadEditedFiles(MainWindowViewModel mainWindowView, string role = "");
}

internal class UploadService : IUploadService
{
  private readonly HttpClient _httpClient;
  private readonly IApiHelper _apiHelper;
  private readonly INotificationService _notificationService;

  public UploadService(HttpClient httpClient, IApiHelper apiHelper, INotificationService notificationService)
  {
    _httpClient = httpClient;
    _apiHelper = apiHelper;
    _notificationService = notificationService;
  }

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
    var filesToRemove = new ObservableCollection<Downloads>();

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

  private static bool DeleteFile(Downloads file, MainWindowViewModel mainWindowViewModel)
  {
    File.Delete(file.FilePath);
    mainWindowViewModel.DownloadedFiles.RemoveAt(mainWindowViewModel.SelectedDownloadedFileIndex);
    JsonHelper.WriteDataToAppData(mainWindowViewModel);
    return true;
  }

  private static bool KeepFile(Downloads file, MainWindowViewModel mainWindowViewModel)
  {
    file.IsEdited = false;
    file.IsKept = true;
    JsonHelper.WriteDataToAppData(mainWindowViewModel);
    return true;
  }

  private async Task<bool> HandleFileRemoval(
    ObservableCollection<Downloads> filesToRemove,
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

      JsonHelper.WriteDataToAppData(mainWindowViewModel);
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
      var progress = new Progress<ProgressInfo>(prog =>
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
        _apiHelper.UploadUrl(mainView.AuthToken),
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
}
