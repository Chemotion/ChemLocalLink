/// <summary>
/// Handles data persistence for JSON files, theme settings, and app configuration
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChemLocalLink.ViewModels;
using Newtonsoft.Json;

namespace ChemLocalLink.Services;

public interface IJsonDataService
{
  void AppendJsonToFile(string path, object jsonObject);
  Task WriteDataToAppData(MainWindowViewModel mainWindowViewModel);
  void SaveCurrentTheme(bool isDarkMode);
  bool LoadCurrentTheme();
}

internal class JsonDataService : IJsonDataService
{
  private readonly string _appDataPath;
  private readonly string _themeFilePath;

  public JsonDataService()
  {
    _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChemLocalLink");
    _themeFilePath = Path.Combine(_appDataPath, "theme.txt");
  }

  public void AppendJsonToFile(string path, object jsonObject)
  {
    string json = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);

    if (File.Exists(path) && !string.IsNullOrEmpty(File.ReadAllText(path)))
    {
      string existingJson = File.ReadAllText(path);

      var existingEntries = JsonConvert.DeserializeObject<List<object>>(existingJson);

      existingEntries.Add(jsonObject);

      json = JsonConvert.SerializeObject(existingEntries, Formatting.Indented);
    }
    else
    {
      var newEntries = new List<object> { jsonObject };

      json = JsonConvert.SerializeObject(newEntries, Formatting.Indented);
    }

    var tmp = path + ".tmp";
    File.WriteAllText(tmp, json);
    File.Move(tmp, path, true);
  }

  public async Task WriteDataToAppData(MainWindowViewModel mainWindowViewModel)
  {
    Directory.CreateDirectory(_appDataPath);
    var jsonFilePath = Path.Combine(_appDataPath, "downloads.json");
    var data = JsonConvert.SerializeObject(
      mainWindowViewModel.DownloadedFiles.Select(d => new
      {
        d.FileId,
        d.FileName,
        d.OriginalFileName,
        d.FilePath,
        d.FileSumOnDownload,
        d.FileSize,
        d.FileDownloadTimeStamp,
        d.IsEdited,
        d.Exp,
        d.Origin,
        d.Path,
        d.Token,
        d.SourceUrl,
        d.IsKept
      }),
      Formatting.Indented
    );
    var tmp = jsonFilePath + ".tmp";
    await File.WriteAllTextAsync(tmp, data);
    if (File.Exists(jsonFilePath))
      File.Delete(jsonFilePath);
    File.Move(tmp, jsonFilePath);
  }

  public void SaveCurrentTheme(bool isDarkMode)
  {
    Directory.CreateDirectory(_appDataPath);
    File.WriteAllText(_themeFilePath, isDarkMode ? "Dark" : "Light");
  }

  public bool LoadCurrentTheme()
  {
    if (File.Exists(_themeFilePath))
    {
      var theme = File.ReadAllText(_themeFilePath);
      return theme == "Dark";
    }
    return false;
  }
}
