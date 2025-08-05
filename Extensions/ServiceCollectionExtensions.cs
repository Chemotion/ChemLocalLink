/// <summary>
/// Configures dependency injection for ChemLocalLink
///
/// Registers services, helpers, and view models in DI container
/// Sets up HttpClient with timeout and custom User-Agent
/// Adds platform-specific notification services
/// Uses singleton for core services and transient for ViewModels
/// Centralizes service registration for consistent lifetime management
/// </summary>

using System;
using System.Net.Http;
using ChemLocalLink.Helpers;
using ChemLocalLink.Services;
using ChemLocalLink.ViewModels;
using DesktopNotifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChemLocalLink.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddApplicationServices(
    this IServiceCollection services,
    INotificationManager? notificationManager = null
  )
  {
    // http client with configuration
    services.AddSingleton<HttpClient>(provider =>
    {
      var httpClient = new HttpClient();
      httpClient.Timeout = TimeSpan.FromMinutes(5); // 5 minute timeout for downloads
      httpClient.DefaultRequestHeaders.Add("User-Agent", "ChemLocalLink/1.0");
      return httpClient;
    });

    // notification manager and service
    if (notificationManager != null)
    {
      services.AddSingleton(notificationManager);
    }
    services.AddSingleton<INotificationService, NotificationService>();

    // services
    services.AddSingleton<IDownloadService, DownloadService>();
    services.AddSingleton<IUploadService, UploadService>();
    services.AddSingleton<IFileService, FileService>();
    services.AddSingleton<ITokenService, TokenService>();
    services.AddSingleton<ITrayService, TrayService>();

    // helpers
    services.AddSingleton<IApiHelper, ApiHelper>();
    services.AddSingleton<IWindowHelper, WindowHelper>();
    services.AddSingleton<IProcessHelper, ProcessHelper>();

    // ViewModels
    services.AddTransient<MainWindowViewModel>();

    return services;
  }
}
