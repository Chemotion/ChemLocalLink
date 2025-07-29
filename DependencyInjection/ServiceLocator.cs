using System;
using Microsoft.Extensions.DependencyInjection;

namespace urlhandler.DependencyInjection;

public static class ServiceLocator
{
  private static IServiceProvider? _serviceProvider;

  public static void Initialize(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
  }

  public static T GetService<T>()
    where T : notnull
  {
    if (_serviceProvider == null)
      throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");

    return _serviceProvider.GetRequiredService<T>();
  }

  public static object GetService(Type serviceType)
  {
    if (_serviceProvider == null)
      throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");

    return _serviceProvider.GetRequiredService(serviceType);
  }

  public static T? GetOptionalService<T>()
    where T : class
  {
    if (_serviceProvider == null)
      return null;

    return _serviceProvider.GetService<T>();
  }

  public static bool IsInitialized => _serviceProvider != null;

  public static void Reset()
  {
    _serviceProvider = null;
  }
}
