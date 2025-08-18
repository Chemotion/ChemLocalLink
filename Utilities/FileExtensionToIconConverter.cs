/// <summary>
/// Converts file extensions to a FontAwesome icon and color.
/// </summary>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ChemLocalLink.Utilities;

public class FileExtensionToIconConverter : IValueConverter
{
  private const string DefaultIcon = "fa-regular fa-file";
  private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#6C757D"));

  private static readonly Dictionary<string, (string Icon, string Color)> ExtensionToIcon = BuildMap();

  private static Dictionary<string, (string Icon, string Color)> BuildMap()
  {
    var m = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

    void Add(string color, string icon, params string[] exts)
    {
      foreach (var e in exts)
        m[e.StartsWith('.') ? e : "." + e] = (icon, color);
    }

    const string Red = "#E74C3C",
      Orange = "#F39C12",
      Yellow = "#F1C40F",
      Green = "#27AE60",
      Teal = "#1ABC9C",
      Blue = "#2980B9",
      Indigo = "#3F51B5",
      Purple = "#8E44AD",
      Gray = "#6C757D";

    Add(Red, "fa-solid fa-file-pdf", ".pdf");
    Add(Blue, "fa-solid fa-file-word", ".doc", ".docx", ".odt", ".pages");
    Add(Green, "fa-solid fa-file-excel", ".xls", ".xlsx", ".ods", ".csv", ".tsv");
    Add(Orange, "fa-solid fa-file-powerpoint", ".ppt", ".pptx", ".odp", ".key");
    Add(Gray, "fa-solid fa-file-lines", ".txt", ".rtf", ".log", ".out", ".readme");

    Add(Yellow, "fa-brands fa-js", ".js");
    Add(Blue, "fa-solid fa-file-code", ".ts", ".xaml", ".axaml");
    Add(Indigo, "fa-brands fa-python", ".py");
    Add(Purple, "fa-solid fa-file-code", ".cs", ".cpp", ".c", ".h", ".java", ".go", ".rb", ".swift", ".kt");
    Add(Orange, "fa-solid fa-brackets-curly", ".json", ".xml");
    Add(Gray, "fa-solid fa-file-lines", ".yml", ".yaml", ".ini", ".cfg", ".conf");

    Add(Purple, "fa-solid fa-file-image", ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".svg", ".webp", ".ico");
    Add(Orange, "fa-solid fa-file-audio", ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a");
    Add(Red, "fa-solid fa-file-video", ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm");

    Add(Orange, "fa-solid fa-file-zipper", ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz");

    Add(Gray, "fa-solid fa-gear", ".exe", ".dll", ".so", ".dylib");
    Add(Blue, "fa-solid fa-box", ".msi", ".appx", ".vhd");
    Add(Red, "fa-solid fa-box", ".rpm");
    Add(Teal, "fa-solid fa-compact-disc", ".iso", ".img");

    Add(Blue, "fa-solid fa-database", ".db", ".sqlite", ".sqlite3", ".sql");

    Add(Green, "fa-solid fa-flask-vial", ".jdx");
    Add(Teal, "fa-solid fa-chart-line", ".dx");
    Add(Purple, "fa-solid fa-atom", ".mnova");
    Add(Green, "fa-solid fa-flask", ".mol", ".sdf", ".cml", ".cdx", ".cdxml", ".xyz");

    return m;
  }

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    var isColorRequest = string.Equals(parameter?.ToString(), "color", StringComparison.OrdinalIgnoreCase);
    var ext = value is string s ? Path.GetExtension(s) : "";

    if (!string.IsNullOrEmpty(ext) && ExtensionToIcon.TryGetValue(ext, out var iconData))
    {
      return isColorRequest ? new SolidColorBrush(Color.Parse(iconData.Color)) : iconData.Icon;
    }

    return isColorRequest ? DefaultBrush : DefaultIcon;
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
    throw new NotSupportedException();
}
