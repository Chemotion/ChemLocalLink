/// <summary>
/// Code-behind for the main application window in MVVM pattern
/// </summary>

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ChemLocalLink.ViewModels;

namespace ChemLocalLink.Views;

public partial class MainWindowView : Window
{
  public MainWindowView()
  {
    InitializeComponent();
  }

  private async void ExportSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is not MainWindowViewModel vm)
      return;

    var file = await StorageProvider.SaveFilePickerAsync(
      new FilePickerSaveOptions
      {
        Title = "Export Session",
        SuggestedFileName = $"session-{DateTime.Now:yyyyMMdd-HHmmss}.chemlocallink",
        ShowOverwritePrompt = true,
        FileTypeChoices = new[]
        {
          new FilePickerFileType("ChemLocalLink Session") { Patterns = new[] { "*.chemlocallink" } }
        }
      }
    );
    if (file == null)
      return;

    var path = file.Path.LocalPath;
    var ok = await vm.ExportSession(path);
    vm.Status = ok ? "Session exported" : "Export failed";
  }

  private async void ImportSession_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is not MainWindowViewModel vm)
      return;

    var files = await StorageProvider.OpenFilePickerAsync(
      new FilePickerOpenOptions
      {
        Title = "Import Session",
        AllowMultiple = false,
        FileTypeFilter = new[]
        {
          new FilePickerFileType("ChemLocalLink Session") { Patterns = new[] { "*.chemlocallink" } }
        }
      }
    );
    if (files == null || files.Count == 0)
      return;

    var path = files[0].Path.LocalPath;
    if (!File.Exists(path))
      return;

    var ok = await vm.ImportSession(path);
    vm.Status = ok ? "Session imported" : "Import failed";
  }
}
