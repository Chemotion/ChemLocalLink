using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using ChemLocalLink.Extensions;
using ChemLocalLink.Services;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Helpers;

public interface IProcessHelper
{
  Task HandleProcess(MainWindowViewModel mainWindowView, string url);
}

internal class ProcessHelper : IProcessHelper
{
  private readonly IDownloadService _downloadService;
  private readonly ITokenService _tokenService;
  private readonly INotificationService _notificationService;
  private readonly IFileService _fileService;

  public ProcessHelper(
    IDownloadService downloadService,
    ITokenService tokenService,
    INotificationService notificationService,
    IFileService fileService
  )
  {
    _downloadService = downloadService;
    _tokenService = tokenService;
    _notificationService = notificationService;
    _fileService = fileService;
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

        if (mainWindowView.Url.ToLower().Contains("url="))
        {
          var uri = new Uri(mainWindowView.Url);
          string? parm = HttpUtility.ParseQueryString(uri.Query).Get("url");
          if (!string.IsNullOrEmpty(parm))
          {
            mainWindowView.Url = parm;
            _url = parm;
            if (mainWindowView.Url != _url)
            {
              mainWindowView.SelectedUrl = _url;
              mainWindowView.Url = _url;
            }
          }
        }

        if (!string.IsNullOrEmpty(_url) && mainWindowView.Url != _url)
          mainWindowView.Url = _url;
        var token = _url.ExtractAuthToken();
        var downloadedFile = await _downloadService.DownloadFile(mainWindowView, token!);
        mainWindowView._filePath = downloadedFile?.filePath ?? null;
        if (mainWindowView._filePath == null)
        {
          mainWindowView.Status = NotificationService.Messages.DownloadFail;
          await _notificationService.ShowNotificationAsync(mainWindowView.Status);
          return;
        }

        await _fileService.ProcessFile(mainWindowView._filePath, mainWindowView, downloadedFile?.originalName ?? "");
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
      throw;
    }
  }
}
