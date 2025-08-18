/// <summary>
/// Hierarchical file organization supporting folder structures and origin grouping.
/// </summary>

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChemLocalLink.Models;

public partial class OriginGroupModel : ObservableObject
{
  public string Origin { get; set; } = "Unknown";

  [ObservableProperty]
  private ObservableCollection<DownloadModel> files = [];

  [ObservableProperty]
  private ObservableCollection<FolderModel> folders = [];

  [ObservableProperty]
  private ObservableCollection<object> items = [];

  public OriginGroupModel()
  {
    Files.CollectionChanged += OnCollectionChanged;
    Folders.CollectionChanged += OnCollectionChanged;
    UpdateItems();
  }

  private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    UpdateItems();
  }

  private void UpdateItems()
  {
    Items.Clear();
    foreach (var folder in Folders)
      Items.Add(folder);
    foreach (var file in Files)
      Items.Add(file);
  }

  public override string ToString() => Origin;
}

public partial class FolderModel : ObservableObject
{
  public string Name { get; set; } = string.Empty;
  public string FullPath { get; set; } = string.Empty;

  [ObservableProperty]
  private ObservableCollection<DownloadModel> files = [];

  [ObservableProperty]
  private ObservableCollection<FolderModel> subFolders = [];

  [ObservableProperty]
  private ObservableCollection<object> items = [];

  public bool IsCollapsed { get; set; } = true;

  public FolderModel()
  {
    Files.CollectionChanged += OnCollectionChanged;
    SubFolders.CollectionChanged += OnCollectionChanged;
    UpdateItems();
  }

  private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    UpdateItems();
  }

  private void UpdateItems()
  {
    Items.Clear();
    foreach (var subFolder in SubFolders)
      Items.Add(subFolder);
    foreach (var file in Files)
      Items.Add(file);
  }

  public override string ToString() => Name;
}
