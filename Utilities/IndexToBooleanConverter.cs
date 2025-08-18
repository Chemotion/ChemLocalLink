/// <summary>
/// Converts integer index to boolean for UI bindings
/// </summary>

using System;
using Avalonia.Data.Converters;

namespace ChemLocalLink.Utilities;

public class IndexToBooleanConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
  {
    if (value is int index)
    {
      return index >= 0;
    }
    return false;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
