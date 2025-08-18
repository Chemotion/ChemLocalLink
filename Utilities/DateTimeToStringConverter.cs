/// <summary>
/// Converts DateTime and Unix timestamps to formatted strings
/// </summary>

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ChemLocalLink.Utilities;

public class DateTimeToStringConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is DateTime dateTime)
    {
      return dateTime.ToString("dd/MM/yyyy h:mm tt", culture);
    }
    else if (value is long epoch)
    {
      DateTime dateTimeFromEpoch = DateTimeOffset.FromUnixTimeSeconds(epoch).DateTime;
      DateTime localDateTime = dateTimeFromEpoch.ToLocalTime();
      DateTime now = DateTime.Now;

      if (targetType == typeof(IBrush))
      {
        if (now > localDateTime)
          return new SolidColorBrush(Color.FromRgb(231, 76, 60));

        TimeSpan timeRemaining = localDateTime - now;
        if (timeRemaining.TotalHours < 24)
          return new SolidColorBrush(Color.FromRgb(255, 193, 7));

        return new SolidColorBrush(Color.FromRgb(39, 174, 96));
      }

      string? paramStr = parameter?.ToString();

      if (paramStr == "short")
      {
        if (now > localDateTime)
          return "Expired";

        TimeSpan timeRemaining = localDateTime - now;
        if (timeRemaining.TotalDays >= 1)
          return $"{(int)timeRemaining.TotalDays}d left";
        else if (timeRemaining.TotalHours >= 1)
          return $"{(int)timeRemaining.TotalHours}h left";
        else
          return $"{(int)timeRemaining.TotalMinutes}m left";
      }

      if (paramStr == "tooltip")
      {
        if (now > localDateTime)
          return $"File upload expired on {localDateTime.ToString("dd/MM/yyyy h:mm tt", culture)}";

        TimeSpan timeRemaining = localDateTime - now;
        string timeRemainingText;

        if (timeRemaining.TotalDays >= 1)
          timeRemainingText = $"{(int)timeRemaining.TotalDays} day(s) and {timeRemaining.Hours} hour(s)";
        else if (timeRemaining.TotalHours >= 1)
          timeRemainingText = $"{(int)timeRemaining.TotalHours} hour(s) and {timeRemaining.Minutes} minute(s)";
        else
          timeRemainingText = $"{(int)timeRemaining.TotalMinutes} minute(s)";

        return $"File upload expires in {timeRemainingText}\nExpiration date: {localDateTime.ToString("dd/MM/yyyy h:mm tt", culture)}";
      }

      return now < localDateTime
        ? "File upload will expire on " + localDateTime.ToString("dd/MM/yyyy h:mm tt", culture)
        : "File upload expired.";
    }
    return value!;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
