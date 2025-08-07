/// <summary>
/// Builds Chemotion API URLs for download and upload
/// </summary>

using System;
using ChemLocalLink.Services;

namespace ChemLocalLink.Helpers;

public interface IApiHelper
{
  string ApiHost { get; set; }
  string ApiEndpoint { get; set; }
  string DownloadUrl(string token);
  string UploadUrl(string authToken);
  void SetFromUrl(string url);
}

internal class ApiHelper : IApiHelper
{
  private string? _apiHost = "";
  private string? _apiEndpoint = "";

  public string ApiHost
  {
    get => _apiHost ?? "";
    set => _apiHost = value;
  }

  public string ApiEndpoint
  {
    get => _apiEndpoint ?? "";
    set => _apiEndpoint = value;
  }

  public ApiHelper() { }

  public void SetFromUrl(string url)
  {
    var uri = new Uri(url);
    ApiHost = $"{uri.Scheme}://{uri.Host}";

    var path = uri.AbsolutePath;
    var lastSlashIndex = path.LastIndexOf('/');
    if (lastSlashIndex > 0)
    {
      ApiEndpoint = path[1..lastSlashIndex];
    }
  }

  public string DownloadUrl(string token) => $"{ApiHost}/{ApiEndpoint}/{token}";

  public string UploadUrl(string authToken) => $"{ApiHost}/{ApiEndpoint}/{authToken}";
}
