using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ChemLocalLink.Extensions;
using ChemLocalLink.Helpers;
using ChemLocalLink.Models;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Services;

public interface IDownloadService
{
  Task<(string filePath, string originalName)?> DownloadFile(MainWindowViewModel mainWindowView, string token);
}

internal class DownloadService : IDownloadService
{
  private readonly HttpClient _httpClient;
  private readonly IApiHelper _apiHelper;
  private readonly INotificationService _notificationService;

  public DownloadService(HttpClient httpClient, IApiHelper apiHelper, INotificationService notificationService)
  {
    _httpClient = httpClient;
    _apiHelper = apiHelper;
    _notificationService = notificationService;
  }

  public async Task<(string filePath, string originalName)?> DownloadFile(
    MainWindowViewModel mainWindowView,
    string authToken
  )
  {
    try
    {
      var token = authToken.Length < 1 ? mainWindowView.Url![(mainWindowView.Url!.LastIndexOf('/') + 1)..] : authToken;
      mainWindowView.AuthToken = token;
      var url = new Uri(mainWindowView.Url!);
      _apiHelper.ApiHost = $"{url.Scheme}://{url.Host}";
      var downloadUrl = _apiHelper.DownloadUrl(token);
      mainWindowView.Status = NotificationService.Messages.Downloading;
      var progress = new Progress<ProgressInfo>(progressInfo =>
      {
        mainWindowView.Status =
          $"Downloaded {progressInfo.BytesRead.FormatBytes()} out of {progressInfo.TotalBytesExpected?.FormatBytes() ?? "0"}.";
      });

      var (response, fileContentBytes) = await _httpClient.GetWithProgressAsync(downloadUrl, progress);
      var headers = response.Content.Headers;

      var _headers = headers.ToImmutableDictionary();
      var contentDisposition = _headers["Content-Disposition"].FirstOrDefault();

      if (!response.IsSuccessStatusCode || contentDisposition == null || !contentDisposition.Contains("filename"))
      {
        mainWindowView.Status = NotificationService.Messages.DownloadFail;
        await _notificationService.ShowNotificationAsync(mainWindowView.Status);

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
      await fileStream.WriteAsync(fileContentBytes.AsMemory(0, fileContentBytes.Length));
      return (filePath, originalName);
    }
    catch (Exception ex)
    {
      var errorMessage =
        ex is IOException ? NotificationService.Messages.FileAccessError : NotificationService.Messages.UnExpectedError;
      mainWindowView.Status = errorMessage;
      await _notificationService.ShowNotificationAsync(mainWindowView.Status);
      return null;
    }
  }
}
