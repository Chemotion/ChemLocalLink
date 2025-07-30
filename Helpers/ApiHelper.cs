using ChemLocalLink.Services;

namespace ChemLocalLink.Helpers;

public interface IApiHelper
{
  string ApiHost { get; set; }
  string DownloadUrl(string token);
  string UploadUrl(string authToken);
  string TokenUrl(string? attId, string? appId);
}

internal class ApiHelper : IApiHelper
{
  private string? _apiHost = "";
  private readonly string _downloadEndPoint = "api/v1/public/third_party_apps";
  private readonly string _uploadEndPoint = "api/v1/public/third_party_apps";
  private readonly string _tokenEndPoint = "api/v1/third_party_apps/token";

  public string ApiHost
  {
    get => _apiHost ?? "";
    set => _apiHost = value;
  }

  public ApiHelper() { }

  public string DownloadUrl(string token) => $"{ApiHost}/{_downloadEndPoint}/{token}";

  public string UploadUrl(string authToken) => $"{ApiHost}/{_uploadEndPoint}/{authToken}";

  public string TokenUrl(string? attId, string? appId) => $"{ApiHost}/{_tokenEndPoint}?attID={attId}&appId={appId}";
}
