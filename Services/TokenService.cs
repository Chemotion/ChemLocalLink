using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Threading.Tasks;
using ChemLocalLink.Helpers;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Services;

public interface ITokenService
{
  Task FetchAuthToken(MainWindowViewModel mainWindowView);
  JwtPayload GetTokenParameters(string token);
  long TokenExp(string token);
}

internal class TokenService : ITokenService
{
  private readonly HttpClient _httpClient;
  private readonly IApiHelper _apiHelper;

  public TokenService(HttpClient httpClient, IApiHelper apiHelper)
  {
    _httpClient = httpClient;
    _apiHelper = apiHelper;
  }

  public async Task FetchAuthToken(MainWindowViewModel mainWindowView)
  {
    try
    {
      var tokenParameters = GetTokenParameters(mainWindowView.Url!);
      if (true)
      {
        var response = await _httpClient.GetAsync(
          _apiHelper.TokenUrl(tokenParameters["attID"].ToString(), tokenParameters["appID"].ToString())
        );

        if (response.IsSuccessStatusCode)
        {
          var content = await response.Content.ReadAsStringAsync();

          if (!string.IsNullOrEmpty(content))
          {
            mainWindowView.AuthToken = content;
          }
          else
          {
            throw new InvalidOperationException("Failed to parse auth token from response content.");
          }
        }
        else
        {
          throw new HttpRequestException($"Failed to fetch auth token. Status code: {response.StatusCode}");
        }
      }
    }
    catch (HttpRequestException ex)
    {
      Console.WriteLine($"Error fetching auth token: {ex.Message}");
      throw;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error fetching auth token: {ex.Message}");
      throw;
    }
  }

  public JwtPayload GetTokenParameters(string url)
  {
    var handler = new JwtSecurityTokenHandler();
    return handler.ReadToken(url[(url.LastIndexOf('/') + 1)..]) is not JwtSecurityToken jsonToken
      ? []
      : jsonToken.Payload;
  }

  public long TokenExp(string token)
  {
    return GetTokenParameters(token).Expiration ?? 0;
  }
}
