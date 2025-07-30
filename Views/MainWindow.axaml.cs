/// <summary>
/// Code-behind for the main application window in MVVM pattern
///
/// Initializes Avalonia UI components from MainWindow.axaml
/// Delegates all logic to MainWindowViewModel via data binding
/// Serves as presentation layer with minimal code-behind
/// Used as the main window instance by App.axaml.cs
/// </summary>

using Avalonia.Controls;

namespace ChemLocalLink.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
  }
}
