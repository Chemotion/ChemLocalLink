using System;
using System.Net.Http;
using ChemLocalLink.Services;
using ChemLocalLink.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChemLocalLink.DependencyInjection;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddApplicationServices(this IServiceCollection services)
  {
    // http client with configuration
    services.AddSingleton<HttpClient>(provider =>
    {
      var httpClient = new HttpClient();
      httpClient.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for downloads
      httpClient.DefaultRequestHeaders.Add("User-Agent", "ChemLocalLink/1.0");
      return httpClient;
    });

    // services
    services.AddSingleton<IDownloadService, DownloadService>();
    services.AddSingleton<IUploadService, UploadService>();
    services.AddSingleton<IFileService, FileService>();
    services.AddSingleton<ITokenService, TokenService>();
    services.AddSingleton<ITrayService, TrayService>();

    // ViewModels
    services.AddTransient<MainWindowViewModel>();

    return services;
  }
}
