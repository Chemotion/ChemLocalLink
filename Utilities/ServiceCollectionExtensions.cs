/// <summary>
/// Configures dependency injection for ChemLocalLink
/// </summary>

using System;
using System.Net.Http;
using ChemLocalLink.Services;
using ChemLocalLink.ViewModels;
using DesktopNotifications;
using Microsoft.Extensions.DependencyInjection;

namespace ChemLocalLink.Utilities;

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

    // Services
    if (notificationManager != null)
    {
      services.AddSingleton(notificationManager);
    }
    services.AddSingleton<INotificationService, NotificationService>();
    services.AddSingleton<IApiService, ApiService>();
    services.AddSingleton<IJsonDataService, JsonDataService>();
    services.AddSingleton<IFileOpsService, FileOpsService>();
    services.AddSingleton<IWorkflowService, WorkflowService>();
    services.AddSingleton<ITrayService, TrayService>();
    services.AddSingleton<IWindowService, WindowService>();
    services.AddSingleton<IPathService, PathService>();
    services.AddSingleton<ISessionService, SessionService>();

    // ViewModels
    services.AddTransient<MainWindowViewModel>();

    return services;
  }
}
