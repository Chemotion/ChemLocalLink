using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace urlhandler.Converters;

public class DateTimeToStringConverter : IValueConverter {
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
    if (value is DateTime dateTime) {
      return dateTime.ToString("dd/MM/yyyy h:mm tt");
    }
    return value!;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
    throw new NotImplementedException();
  }
}
