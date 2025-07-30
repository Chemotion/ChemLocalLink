using System;

namespace ChemLocalLink.DependencyInjection;

public interface IServiceLocator
{
  T GetService<T>()
    where T : notnull;
  object GetService(Type serviceType);
  bool IsInitialized { get; }
}
