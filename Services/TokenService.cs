/// <summary>
/// Handles token parsing and validation
/// </summary>

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Services;

public interface ITokenService
{
  JwtPayload GetTokenParameters(string token);
  long TokenExp(string token);
}

internal class TokenService : ITokenService
{
  private readonly HttpClient _httpClient;

  public TokenService(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

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
