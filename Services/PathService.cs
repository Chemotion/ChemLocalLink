/// <summary>
/// Provides cross-platform paths for downloads and configuration, and handles migration.
/// </summary>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ChemLocalLink.Models;
using Newtonsoft.Json;

namespace ChemLocalLink.Services;

public interface IPathService
{
  string GetDownloadDirectory();
  void EnsureDownloadDirectory();
  string GetConfigPath();
  AppConfig LoadConfig();
  void SaveConfig(AppConfig config);
  void MigrateFromLegacyTempDir(IList<DownloadModel> existingDownloads);
}

public class AppConfig
{
  public int Version { get; set; } = 1;
  public string? DownloadDirectory { get; set; }
}

internal class PathService : IPathService
{
  private readonly string _appDataPath;
  private readonly string _configFilePath;
  private const string LegacyTempFolderName = "chemotion"; // legacy temp download folder

  public PathService()
  {
    _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChemLocalLink");
    Directory.CreateDirectory(_appDataPath);
    _configFilePath = Path.Combine(_appDataPath, "config.json");
  }

  public string GetConfigPath() => _configFilePath;

  public AppConfig LoadConfig()
  {
    try
    {
      if (File.Exists(_configFilePath))
      {
        var txt = File.ReadAllText(_configFilePath);
        if (!string.IsNullOrWhiteSpace(txt))
        {
          return JsonConvert.DeserializeObject<AppConfig>(txt) ?? new AppConfig();
        }
      }
    }
    catch { }
    return new AppConfig();
  }

  public void SaveConfig(AppConfig config)
  {
    Directory.CreateDirectory(_appDataPath);
    File.WriteAllText(_configFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
  }

  public string GetDownloadDirectory()
  {
    var cfg = LoadConfig();
    if (!string.IsNullOrWhiteSpace(cfg.DownloadDirectory))
    {
      return cfg.DownloadDirectory!;
    }

    // Determine default
    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    string defaultDir;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      defaultDir = Path.Combine(documents, "ChemLocalLink");
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      // macOS documents folder
      defaultDir = Path.Combine(documents, "ChemLocalLink");
    }
    else // Linux / others
    {
      if (!string.IsNullOrWhiteSpace(documents) && Directory.Exists(documents))
      {
        defaultDir = Path.Combine(documents, "ChemLocalLink");
      }
      else
      {
        defaultDir = Path.Combine(home, "ChemLocalLink");
      }
    }

    cfg.DownloadDirectory = defaultDir;
    SaveConfig(cfg);
    return defaultDir;
  }

  public void EnsureDownloadDirectory()
  {
    var dir = GetDownloadDirectory();
    Directory.CreateDirectory(dir);
  }

  public void MigrateFromLegacyTempDir(IList<DownloadModel> existingDownloads)
  {
    try
    {
      if (existingDownloads == null || existingDownloads.Count == 0)
        return;
      var legacyDir = Path.Combine(Path.GetTempPath(), LegacyTempFolderName);
      if (!Directory.Exists(legacyDir))
        return;

      var targetDir = GetDownloadDirectory();
      Directory.CreateDirectory(targetDir);

      var changed = false;
      foreach (var d in existingDownloads.ToList())
      {
        try
        {
          if (string.IsNullOrWhiteSpace(d.FilePath))
            continue;
          var full = d.FilePath;
          if (!File.Exists(full))
            continue; // already removed

          // only migrate if inside legacy folder
          if (!Path.GetFullPath(full).StartsWith(Path.GetFullPath(legacyDir), StringComparison.OrdinalIgnoreCase))
            continue;

          var fileName = Path.GetFileName(full);
          var destPath = Path.Combine(targetDir, fileName);
          var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
          var ext = Path.GetExtension(fileName);
          int counter = 1;
          while (File.Exists(destPath))
          {
            destPath = Path.Combine(targetDir, $"{nameNoExt}-{counter}{ext}");
            counter++;
          }

          try
          {
            File.Move(full, destPath);
          }
          catch
          {
            // fallback to copy if move fails
            File.Copy(full, destPath, overwrite: false);
          }

          d.FilePath = destPath;
          changed = true;
        }
        catch { }
      }

      if (changed) { }
    }
    catch { }
  }
}
