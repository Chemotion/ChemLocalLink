﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using urlhandler.Extensions;
using urlhandler.Models;
using urlhandler.ViewModels;
using urlhandler.Helpers;

namespace urlhandler.Services;

internal interface IFileService {
  Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName);
}

internal class FileService : IFileService {
  public Task ProcessFile(string? filePath, MainWindowViewModel mainWindowView, string originalName) {
    try {
      if (filePath == null) return Task.CompletedTask;
      WindowHelper.MainWindowViewModel?._fileProcess?.Dispose();

      if (WindowHelper.MainWindowViewModel != null) {
        WindowHelper.MainWindowViewModel._fileProcess = new Process {
          StartInfo = new ProcessStartInfo(filePath) {
            UseShellExecute = true
          }
        };
      }
      else {
        throw new InvalidOperationException("MainWindowViewModel is not initialized.");
      }

      WindowHelper.MainWindowViewModel._fileProcess.Start();

      var random = new Random(2345);

      {
        var id = WindowHelper.MainWindowViewModel.DownloadedFiles?.Any() ?? false
            ? WindowHelper.MainWindowViewModel.DownloadedFiles.Max(f => f.FileId) + 1
            : random.NextInt64(10000, 999999);

        var download = new Downloads() {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = ApiHelper.TokenExp(mainWindowView.Url!)
        };

        File.SetCreationTime(filePath, DateTime.Now);

        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChemLocalLink");
        Directory.CreateDirectory(appDataPath);
        var jsonFilePath = Path.Combine(appDataPath, "downloads.json");

        var jsonObject = new {
          FileId = id,
          FileName = Path.GetFileName(filePath),
          OriginalFileName = originalName,
          FilePath = filePath,
          FileSumOnDownload = filePath.FileCheckSum(),
          FileSize = new FileInfo(filePath).Length.FormatBytes(),
          FileDownloadTimeStamp = File.GetLastWriteTime(filePath),
          IsEdited = false,
          Exp = ApiHelper.TokenExp(mainWindowView.Url!)
        };

        JsonHelper.AppendJsonToFile(jsonFilePath, jsonObject);

        WindowHelper.MainWindowViewModel.DownloadedFiles?.Insert(0, download);
      }

      WindowHelper.MainWindowViewModel.HasFilesDownloaded = mainWindowView.DownloadedFiles.Count > 0;
    }
    catch (Exception ex) {
      Console.WriteLine($"Error processing file: {ex.Message}");
    }

    return Task.CompletedTask;
  }
}
