/// <summary>
/// Manages system notifications across platforms
/// </summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DesktopNotifications;

namespace ChemLocalLink.Services;

public interface INotificationService
{
  Task ShowNotificationAsync(string title, string? body = null);
}

internal class NotificationService : INotificationService
{
  private readonly INotificationManager? _notificationManager;

  // Constants for feedback messages
  public static class Messages
  {
    public const string FileKept = "Kept files can not be uploaded!";
    public const string DownloadFail = "Failed to download file";
    public const string DownloadSuccessful = "File downloaded successfully!";
    public const string Downloading = "Downloading File...";
    public const string FileAccessError = "File access error!";
    public const string FileNotEdited = "Not edited yet!";
    public const string InvalidUrl = "Invalid URL format!";
    public const string Minimize = "Auto-minimize!";
    public const string NetworkError = "Network error!";
    public const string NoDownloads = "No downloaded files";
    public const string TokenFail = "Failed to extract token!";
    public const string UnExpectedError = "Unexpected error!";
    public const string UploadFail = "Failed to upload file(s)";
    public const string UploadSuccessful = "File(s) uploaded successfully";
  }

  private static readonly Dictionary<string, string> _bodyMessages =
    new()
    {
      { Messages.FileKept, "Please download file again to edit and upload it." },
      { Messages.DownloadFail, "URL is false or expired. Try again with a different one." },
      { Messages.DownloadSuccessful, "Heroic action! Please continue like that!" },
      { Messages.UploadSuccessful, "Heroic action! Please continue like that!" },
      { Messages.FileAccessError, "Make sure the file exists and you have sufficient permissions." },
      { Messages.FileNotEdited, "File(s) not edited yet." },
      { Messages.InvalidUrl, "Ensure the URL matches the expected pattern." },
      { Messages.Minimize, "Minimized due to inactivity." },
      { Messages.NetworkError, "Please check your connection or contact support if the problem persists." },
      { Messages.NoDownloads, "There are no downloaded files at the moment." },
      { Messages.UploadFail, "URL is expired. You have to download it again using a new link." },
      { Messages.UnExpectedError, "Unexpected behavior, please report." },
    };

  public NotificationService(INotificationManager? notificationManager = null)
  {
    _notificationManager = notificationManager;
  }

  public async Task ShowNotificationAsync(string title, string? body = null)
  {
    try
    {
      if (_notificationManager == null)
      {
        Debug.WriteLine($"Notification: {title} - {body ?? "No body"}");
        return;
      }

      if (
        (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major >= 10)
        || Environment.OSVersion.Platform == PlatformID.Unix
      )
      {
        var resolvedBody =
          body ?? (_bodyMessages.ContainsKey(title) ? _bodyMessages[title] : "Unexpected behavior, please report.");

        var notification = new Notification
        {
          Title = title,
          Body = resolvedBody,
          BodyImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"),
        };

        await _notificationManager.ShowNotification(notification);
      }
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Error showing notification: {ex.Message}");
    }
  }
}
