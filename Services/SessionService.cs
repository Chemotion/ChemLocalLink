/// <summary>
/// Handles exporting and importing session data (.chemlocallink)
/// </summary>
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ChemLocalLink.Models;
using ChemLocalLink.Utilities;
using ChemLocalLink.ViewModels;
using Newtonsoft.Json;

namespace ChemLocalLink.Services;

public interface ISessionService
{
  Task<bool> ExportSessionAsync(MainWindowViewModel vm, string targetArchivePath);
  Task<bool> ImportSessionAsync(MainWindowViewModel vm, string sourceArchivePath);
}

internal class SessionService : ISessionService
{
  private readonly IPathService _pathService;
  private readonly IJsonDataService _jsonDataService;

  public SessionService(IPathService pathService, IJsonDataService jsonDataService)
  {
    _pathService = pathService;
    _jsonDataService = jsonDataService;
  }

  private record Manifest(int schemaVersion, string appVersion, DateTime exportedAtUtc, int fileCount);

  public async Task<bool> ExportSessionAsync(MainWindowViewModel vm, string targetArchivePath)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(targetArchivePath)!);
      if (File.Exists(targetArchivePath))
        File.Delete(targetArchivePath);

      using var zip = ZipFile.Open(targetArchivePath, ZipArchiveMode.Create);

      var downloads = vm.DownloadedFiles.ToList();
      var existingFiles = downloads
        .Where(d => !string.IsNullOrWhiteSpace(d.FilePath) && File.Exists(d.FilePath))
        .Where(d => !d.FilePath.EndsWith("~")) // exclude backup files
        .ToList();

      // manifest
      var manifest = new Manifest(1, vm.AppVersion, DateTime.UtcNow, existingFiles.Count);
      var manifestEntry = zip.CreateEntry("manifest.json");
      using (var ms = manifestEntry.Open())
      using (var sw = new StreamWriter(ms))
        sw.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));

      // prepare portable downloads.json
      var portableList = existingFiles
        .Select(d => new PortableDownload
        {
          FileId = d.FileId,
          FileName = d.FileName,
          OriginalFileName = d.OriginalFileName,
          FileRelativeName = !string.IsNullOrWhiteSpace(d.Path)
            ? $"{d.Path}/{Path.GetFileName(d.FilePath)}"
            : Path.GetFileName(d.FilePath),
          FileDownloadTimeStamp = d.FileDownloadTimeStamp,
          FileSize = d.FileSize,
          FileSumOnDownload = d.FileSumOnDownload,
          IsEdited = d.IsEdited,
          IsCreated = d.IsCreated,
          IsKept = d.IsKept,
          Exp = d.Exp,
          Origin = d.Origin,
          Path = d.Path,
          Token = d.Token,
          SourceUrl = d.SourceUrl
        })
        .ToList();

      var downloadsEntry = zip.CreateEntry("downloads.json");
      using (var ms = downloadsEntry.Open())
      using (var sw = new StreamWriter(ms))
        sw.Write(JsonConvert.SerializeObject(portableList, Formatting.Indented));

      // add files
      foreach (var d in existingFiles)
      {
        try
        {
          var fileName = Path.GetFileName(d.FilePath);
          string entryPath;

          if (!string.IsNullOrWhiteSpace(d.Path))
          {
            // preserve folder structure
            entryPath = $"files/{d.Path}/{fileName}";
          }
          else
          {
            entryPath = $"files/{fileName}";
          }

          var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
          using var fs = File.OpenRead(d.FilePath);
          using var es = entry.Open();
          await fs.CopyToAsync(es);
        }
        catch { }
      }

      return true;
    }
    catch
    {
      return false;
    }
  }

  private class PortableDownload
  {
    public float FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileRelativeName { get; set; } = string.Empty;
    public DateTime FileDownloadTimeStamp { get; set; }
    public string? FileSize { get; set; }
    public string? FileSumOnDownload { get; set; }
    public bool IsEdited { get; set; }
    public bool IsCreated { get; set; }
    public bool IsKept { get; set; }
    public long Exp { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Token { get; set; }
    public string? SourceUrl { get; set; }
  }

  public async Task<bool> ImportSessionAsync(MainWindowViewModel vm, string sourceArchivePath)
  {
    if (!File.Exists(sourceArchivePath))
      return false;

    try
    {
      using var zip = ZipFile.OpenRead(sourceArchivePath);

      var manifestEntry = zip.GetEntry("manifest.json");
      var downloadsEntry = zip.GetEntry("downloads.json");
      if (manifestEntry == null || downloadsEntry == null)
        return false;

      Manifest? manifest;
      using (var ms = manifestEntry.Open())
      using (var sr = new StreamReader(ms))
      {
        manifest = JsonConvert.DeserializeObject<Manifest>(sr.ReadToEnd());
      }
      if (manifest == null || manifest.schemaVersion != 1)
        return false;

      List<PortableDownload>? portableDownloads;
      using (var ms = downloadsEntry.Open())
      using (var sr = new StreamReader(ms))
      {
        portableDownloads = JsonConvert.DeserializeObject<List<PortableDownload>>(sr.ReadToEnd());
      }
      if (portableDownloads == null)
        return false;

      var targetDir = _pathService.GetDownloadDirectory();
      Directory.CreateDirectory(targetDir);

      var existingChecksums = vm
        .DownloadedFiles.Where(d => d.FileSumOnDownload != null)
        .Select(d => d.FileSumOnDownload)
        .ToHashSet();
      var rand = new Random();
      var added = false;

      foreach (var pd in portableDownloads)
      {
        try
        {
          if (!string.IsNullOrWhiteSpace(pd.FileSumOnDownload) && existingChecksums.Contains(pd.FileSumOnDownload))
            continue; // skip duplicate by checksum

          var sourceFileEntry = zip.GetEntry($"files/{pd.FileRelativeName}");
          if (sourceFileEntry == null)
            continue; // missing file

          // Create the target path including folder structure
          string destPath;
          if (!string.IsNullOrWhiteSpace(pd.Path))
          {
            var folderPath = Path.Combine(targetDir, pd.Path);
            Directory.CreateDirectory(folderPath);
            destPath = Path.Combine(folderPath, Path.GetFileName(pd.FileRelativeName)).NormalizePath();
          }
          else
          {
            destPath = Path.Combine(targetDir, Path.GetFileName(pd.FileRelativeName)).NormalizePath();
          }

          var nameNoExt = Path.GetFileNameWithoutExtension(destPath);
          var ext = Path.GetExtension(destPath);
          var baseDir = Path.GetDirectoryName(destPath)!;
          int counter = 1;
          while (File.Exists(destPath))
          {
            destPath = Path.Combine(baseDir, $"{nameNoExt}-{counter}{ext}").NormalizePath();
            counter++;
          }

          using (var es = sourceFileEntry.Open())
          using (var fs = File.Create(destPath))
            await es.CopyToAsync(fs);

          var finalFileName = Path.GetFileName(destPath);

          // create new model
          var model = new DownloadModel
          {
            FileId = pd.FileId == 0 ? rand.NextInt64(10000, 999999) : pd.FileId,
            FileName = finalFileName,
            OriginalFileName = pd.OriginalFileName,
            FilePath = destPath,
            FileDownloadTimeStamp = pd.FileDownloadTimeStamp,
            FileSize = new FileInfo(destPath).Length.FormatBytes(),
            FileSumOnDownload = pd.FileSumOnDownload,
            IsEdited = false,
            IsCreated = pd.IsCreated,
            IsKept = pd.IsKept,
            Exp = pd.Exp,
            Origin = pd.Origin,
            Path = pd.Path,
            Token = pd.Token,
            SourceUrl = pd.SourceUrl
          };

          vm.DownloadedFiles.Insert(0, model);
          if (!string.IsNullOrWhiteSpace(model.FileSumOnDownload))
            existingChecksums.Add(model.FileSumOnDownload);
          added = true;
        }
        catch { }
      }

      if (added)
      {
        vm.HasFilesDownloaded = vm.DownloadedFiles.Count > 0;
        vm.RebuildGroups();
        await _jsonDataService.WriteDataToAppData(vm);
      }

      return added;
    }
    catch
    {
      return false;
    }
  }
}
