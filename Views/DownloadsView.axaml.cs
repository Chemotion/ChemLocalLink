/// <summary>
/// Code-behind for Downloads user control displaying downloaded files
///
/// Initializes UI components and integrates with MainWindowViewModel
/// Supports context menu actions like upload, delete, and open
/// Binds to DownloadedFiles collection with visual status indicators
/// Used in MainWindow as a reusable UI component
/// </summary>

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ChemLocalLink.Views;

public partial class DownloadsView : UserControl
{
  public DownloadsView()
  {
    InitializeComponent();
  }
}
