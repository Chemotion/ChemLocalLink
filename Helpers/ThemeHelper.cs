/// <summary>
/// Saves and loads user theme preference to %AppData%/ChemLocalLink/theme.txt (Dark/Light)
/// </summary>

using System;
using System.IO;

namespace ChemLocalLink.Helpers;

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
