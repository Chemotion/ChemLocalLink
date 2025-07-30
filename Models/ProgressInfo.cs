/// <summary>
/// Represents progress of file transfer operations
///
/// Tracks bytes read, total expected, and percentage complete
/// Supports indeterminate progress with unknown total size
/// Immutable and thread-safe for async reporting
/// Used in HTTP transfers to update UI progress bars
/// </summary>

namespace ChemLocalLink.Models;

public class ProgressInfo(long bytesRead, long? totalBytesExpected, double percentage)
{
  public long BytesRead { get; } = bytesRead;
  public long? TotalBytesExpected { get; } = totalBytesExpected;
  public double Percentage { get; } = percentage;
}
