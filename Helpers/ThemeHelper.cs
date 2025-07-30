/// <summary>
/// Saves and loads user theme preference (Dark/Light)
///
/// Persists theme to %AppData%/ChemLocalLink/theme.txt
/// Restores theme across app sessions with default fallback
/// Creates settings directory if missing
/// Used by MainWindowViewModel for theme management
/// </summary>

using System;
using System.IO;

public static class Theme
{
  private static string settingsFilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "ChemLocalLink",
    "theme.txt"
  );

  public static void SaveCurrentTheme(bool isDarkMode)
  {
    var directory = Path.GetDirectoryName(settingsFilePath);
    if (!Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory!);
    }
    File.WriteAllText(settingsFilePath, isDarkMode ? "Dark" : "Light");
  }

  public static bool LoadCurrentTheme()
  {
    if (File.Exists(settingsFilePath))
    {
      var theme = File.ReadAllText(settingsFilePath);
      return theme == "Dark";
    }
    return false;
  }
}
