/// <summary>
/// Handles Chemotion API URLs and JWT token operations
/// </summary>

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;

namespace ChemLocalLink.Services;

public interface IApiService
{
  string ApiHost { get; set; }
  string ApiEndpoint { get; set; }
  string DownloadUrl(string token);
  string UploadUrl(string authToken);
  void SetFromUrl(string url);
  JwtPayload GetTokenParameters(string token);
  long TokenExp(string token);
}

internal class ApiService : IApiService
{
  private readonly HttpClient _httpClient;
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

  public ApiService(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

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

  public JwtPayload GetTokenParameters(string url)
  {
    var handler = new JwtSecurityTokenHandler();
    var token = url[(url.LastIndexOf('/') + 1)..];

    return handler.ReadToken(token) is not JwtSecurityToken jsonToken ? [] : jsonToken.Payload;
  }

  public long TokenExp(string token)
  {
    return GetTokenParameters(token).Expiration ?? 0;
  }
}
