/// <summary>
/// Represents progress of file transfer operations
/// </summary>

namespace ChemLocalLink.Models;

public class ProgressModel(long bytesRead, long? totalBytesExpected, double percentage)
{
  public long BytesRead { get; } = bytesRead;
  public long? TotalBytesExpected { get; } = totalBytesExpected;
  public double Percentage { get; } = percentage;
}
