/// <summary>
/// Converter to determine if a file is currently selected and return appropriate background
/// </summary>

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ChemLocalLink.Models;

namespace ChemLocalLink.Utilities;

public class FileSelectionConverter : IMultiValueConverter
{
  public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
  {
    if (values.Count != 2 || values[0] is not DownloadModel currentFile)
      return Brushes.Transparent;

    if (values[1] is not DownloadModel selectedFile)
      return Brushes.Transparent;

    return currentFile.FileId == selectedFile.FileId
      ? new SolidColorBrush(Color.FromArgb(80, 0, 120, 215))
      : Brushes.Transparent;
  }
}
