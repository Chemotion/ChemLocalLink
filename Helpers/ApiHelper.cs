using urlhandler.DependencyInjection;
using urlhandler.Services;

namespace urlhandler.Helpers;

internal abstract class ApiHelper
{
  internal static string? apiHost = "";
  private static string? DownloadEndPoint = "api/v1/public/third_party_apps";
  private static string? UploadEndPoint = "api/v1/public/third_party_apps";
  private static string? TokenEndPoint = "api/v1/third_party_apps/token";

  public static string DownloadUrl(string token) => $"{apiHost}/{DownloadEndPoint}/{token}";

  public static string UploadUrl(string authToken) => $"{apiHost}/{UploadEndPoint}/{authToken}";

  public static string TokenUrl(string? attId, string? appId) =>
    $"{apiHost}/{TokenEndPoint}?attID={attId}&appId={appId}";

  public static long TokenExp(string token)
  {
    var tokenService = ServiceLocator.GetService<ITokenService>();
    return tokenService.GetTokenParameters(token).Expiration ?? 0;
  }
}
