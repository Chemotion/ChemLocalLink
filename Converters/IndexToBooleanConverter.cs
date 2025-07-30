/// <summary>
/// Converts integer index to boolean for UI bindings
///
/// Returns true if index >= 0, false otherwise
/// Used to enable or show elements based on selection
/// ConvertBack not supported
/// Applied in XAML bindings for selection-dependent UI logic
/// </summary>

using System;
using Avalonia.Data.Converters;

namespace ChemLocalLink.Converters;

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
