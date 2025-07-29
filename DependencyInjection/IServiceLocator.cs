using System;

namespace urlhandler.DependencyInjection;

public interface IServiceLocator
{
  T GetService<T>()
    where T : notnull;
  object GetService(Type serviceType);
  bool IsInitialized { get; }
}
