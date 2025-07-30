/// <summary>
/// Manages local files, tracking, and download history persistence
///
/// Opens downloaded files with default apps and assigns unique IDs
/// Calculates file checksums for edit detection
/// Saves metadata to %AppData%/ChemLocalLink/downloads.json
/// Tracks file size, timestamps, and token expiration
/// Ensures persistent and editable file monitoring across sessions
/// </summary>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChemLocalLink.Extensions;
using ChemLocalLink.Helpers;
using ChemLocalLink.Models;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Services;

public interface IFileService
{
  Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName);
}

internal class FileService : IFileService
{
  private readonly ITokenService _tokenService;

  public FileService(ITokenService tokenService)
  {
    _tokenService = tokenService;
  }

  public Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName)
  {
    try
    {
      if (filePath == null)
        return Task.CompletedTask;
      mainWindowView?._fileProcess?.Dispose();

      if (mainWindowView != null)
      {
        mainWindowView._fileProcess = new Process
        {
          StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true },
        };
      }
      else
      {
        throw new InvalidOperationException("MainWindowViewModel is not initialized.");
      }

      mainWindowView._fileProcess.Start();

      var random = new Random(2345);

      {
        var id =
          mainWindowView.DownloadedFiles?.Any() ?? false
            ? mainWindowView.DownloadedFiles.Max(f => f.FileId) + 1
            : random.NextInt64(10000, 999999);

        var download = new Downloads()
        {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = _tokenService.TokenExp(mainWindowView.Url!),
        };

        File.SetCreationTime(filePath, DateTime.Now);

        var appDataPath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          "ChemLocalLink"
        );
        Directory.CreateDirectory(appDataPath);
        var jsonFilePath = Path.Combine(appDataPath, "downloads.json");

        var jsonObject = new
        {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = _tokenService.TokenExp(mainWindowView.Url!),
        };

        JsonHelper.AppendJsonToFile(jsonFilePath, jsonObject);

        mainWindowView.DownloadedFiles?.Insert(0, download);
      }

      if (mainWindowView != null)
      {
        mainWindowView.HasFilesDownloaded = (mainWindowView.DownloadedFiles?.Count ?? 0) > 0;
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error processing file: {ex.Message}");
    }

    return Task.CompletedTask;
  }
}
