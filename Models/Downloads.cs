/// <summary>
/// Represents a downloaded file with tracking and metadata
///
/// Includes file name, path, size, timestamp, and checksum
/// Tracks edit status, keep status, and token expiration
/// Implements observable properties for real-time UI updates
/// Persisted in downloads.json for session restoration
/// </summary>

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChemLocalLink.Models;

public partial class Downloads : ObservableObject
{
  // FileId to keep track of edited files in a list
  public float FileId { get; set; }
  public string FileName { get; set; } = string.Empty;
  public string OriginalFileName { get; set; } = string.Empty;
  public string FilePath { get; set; } = string.Empty;
  public DateTime FileDownloadTimeStamp { get; set; }

  [ObservableProperty]
  private string? _fileSize;
  public string? FileSumOnDownload { get; set; }

  [ObservableProperty]
  private bool _isEdited;

  [ObservableProperty]
  private bool _isKept;

  [ObservableProperty]
  private long _exp;
}
