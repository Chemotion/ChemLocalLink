/// <summary>
/// Group of downloaded files by origin host
/// </summary>

using System.Collections.ObjectModel;

namespace ChemLocalLink.Models;

public class OriginGroupModel
{
  public string Origin { get; set; } = "Unknown";
  public ObservableCollection<DownloadModel> Files { get; set; } = [];

  public override string ToString() => Origin;
}
