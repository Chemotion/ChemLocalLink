/// <summary>
/// Orchestrates the main workflow for processing chemotion:// URLs and coordinating file operations
/// </summary>

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using ChemLocalLink.Utilities;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Services;

public interface IWorkflowService
{
  Task HandleProcess(MainWindowViewModel mainWindowView, string url);
}

internal class WorkflowService : IWorkflowService
{
  private readonly IFileOpsService _fileOpsService;
  private readonly INotificationService _notificationService;

  public WorkflowService(IFileOpsService fileOpsService, INotificationService notificationService)
  {
    _fileOpsService = fileOpsService;
    _notificationService = notificationService;
  }

  public async Task HandleProcess(MainWindowViewModel mainWindowView, string _url)
  {
    try
    {
      if (mainWindowView.IsAlreadyProcessing == false)
      {
        if (!Uri.TryCreate(mainWindowView.Url, UriKind.Absolute, out _))
        {
          mainWindowView.Status = NotificationService.Messages.InvalidUrl;
          await _notificationService.ShowNotificationAsync(mainWindowView.Status);

          return;
        }

        if (!string.IsNullOrEmpty(_url) && mainWindowView.Url != _url)
          mainWindowView.Url = _url;
        var token = _url.ExtractAuthToken();
        var downloadedFile = await _fileOpsService.DownloadFile(mainWindowView, token!);
        mainWindowView._filePath = downloadedFile?.filePath ?? null;
        if (mainWindowView._filePath == null)
        {
          mainWindowView.Status = NotificationService.Messages.DownloadFail;
          await _notificationService.ShowNotificationAsync(mainWindowView.Status);
          return;
        }

        await _fileOpsService.ProcessFile(mainWindowView._filePath, mainWindowView, downloadedFile?.originalName ?? "");

        mainWindowView.DeepLinkPath = null;

        mainWindowView.Status = NotificationService.Messages.DownloadSuccessful;
        await _notificationService.ShowNotificationAsync(mainWindowView.Status);
      }
      else
      {
        mainWindowView.Status = NotificationService.Messages.FileAccessError;
        await _notificationService.ShowNotificationAsync(mainWindowView.Status);
      }
    }
    catch (HttpRequestException)
    {
      mainWindowView.Status = NotificationService.Messages.NetworkError;
      await _notificationService.ShowNotificationAsync(mainWindowView.Status);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in Process method: {ex.Message}");
      Console.WriteLine($"Stack trace: {ex.StackTrace}");
      throw;
    }
  }
}
