using System.Collections.ObjectModel;
using CopyPaste.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace CopyPaste.App;

public sealed class ProtectedFolderPickerWindow : Window
{
    private readonly string _resultFile;
    private readonly bool _english;
    private readonly ObservableCollection<FolderEntry> _entries = [];
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ListView _list = new()
    {
        SelectionMode = ListViewSelectionMode.Multiple,
        IsMultiSelectCheckBoxEnabled = true
    };
    private readonly TextBox _pathBox = new();
    private readonly TextBlock _status = new();
    private readonly CheckBox _selectCurrent = new();
    private readonly Button _confirmButton = new();
    private string? _currentPath;
    private bool _refreshingSelection;
    private int _navigationVersion;

    public ProtectedFolderPickerWindow(string resultFile, bool english = false)
    {
        _resultFile = resultFile;
        _english = english;
        Title = T("CopyPaste — Korumalı kaynakları seç", "CopyPaste — Select protected sources");
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        AppWindow.GetFromWindowId(windowId).Resize(new Windows.Graphics.SizeInt32(800, 660));

        _pathBox.PlaceholderText = T("Klasör yolunu yazabilir veya listeden gezebilirsiniz",
            "Type a folder path or browse the list");
        var goButton = new Button { Content = T("Git", "Go") };
        goButton.Click += async (_, _) => await NavigateAsync(_pathBox.Text);
        var upButton = new Button { Content = T("↑ Üst klasör", "↑ Parent folder") };
        upButton.Click += async (_, _) => await NavigateAsync(
            _currentPath is null ? null : Directory.GetParent(_currentPath)?.FullName);
        var pathRow = new Grid { ColumnSpacing = 8 };
        pathRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        pathRow.Children.Add(_pathBox);
        Grid.SetColumn(goButton, 1);
        pathRow.Children.Add(goButton);
        Grid.SetColumn(upButton, 2);
        pathRow.Children.Add(upButton);

        _selectCurrent.Content = T("Geçerli klasörü seçime ekle", "Add the current folder to the selection");
        _selectCurrent.Checked += (_, _) => SetCurrentPathSelected(true);
        _selectCurrent.Unchecked += (_, _) => SetCurrentPathSelected(false);
        _list.ItemsSource = _entries;
        _list.DisplayMemberPath = nameof(FolderEntry.Label);
        _list.SelectionChanged += List_SelectionChanged;
        _list.DoubleTapped += async (_, _) =>
        {
            if (_list.SelectedItem is FolderEntry selected)
                await NavigateAsync(selected.Path);
        };

        _confirmButton.Content = T("Seçimleri kullan", "Use selected folders");
        _confirmButton.HorizontalAlignment = HorizontalAlignment.Right;
        _confirmButton.IsEnabled = false;
        _confirmButton.Click += async (_, _) =>
        {
            if (_selectedPaths.Count == 0)
                return;
            var json = ProtectedFolderSelectionSerializer.Serialize(_selectedPaths);
            await File.WriteAllTextAsync(_resultFile, json);
            Close();
        };
        var cancelButton = new Button { Content = T("İptal", "Cancel") };
        cancelButton.Click += (_, _) => Close();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_confirmButton);

        var panel = new Grid { Padding = new Thickness(20), RowSpacing = 12 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.Children.Add(pathRow);
        Grid.SetRow(_selectCurrent, 1);
        panel.Children.Add(_selectCurrent);
        Grid.SetRow(_list, 2);
        panel.Children.Add(_list);
        Grid.SetRow(_status, 3);
        panel.Children.Add(_status);
        Grid.SetRow(buttons, 4);
        panel.Children.Add(buttons);
        Content = panel;
        _ = NavigateAsync(null);
    }

    private async Task NavigateAsync(string? path)
    {
        var navigationVersion = ++_navigationVersion;
        _status.Text = T("Klasörler yükleniyor…", "Loading folders…");
        try
        {
            string? fullPath = null;
            IReadOnlyList<FolderEntry> entries;
            if (string.IsNullOrWhiteSpace(path))
            {
                entries = await Task.Run<IReadOnlyList<FolderEntry>>(() => DriveInfo.GetDrives()
                    .Where(drive => drive.IsReady)
                    .Select(drive => new FolderEntry(drive.RootDirectory.FullName,
                        $"{drive.Name}  {drive.VolumeLabel}"))
                    .ToArray());
            }
            else
            {
                fullPath = Path.GetFullPath(path.Trim());
                entries = await Task.Run<IReadOnlyList<FolderEntry>>(() => Directory
                    .EnumerateDirectories(fullPath)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                    .Select(directory => new FolderEntry(directory,
                        Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))))
                    .ToArray());
            }
            if (navigationVersion != _navigationVersion)
                return;

            _refreshingSelection = true;
            _entries.Clear();
            foreach (var entry in entries)
                _entries.Add(entry);
            _currentPath = fullPath;
            _pathBox.Text = fullPath ?? string.Empty;
            foreach (var entry in _entries.Where(entry => _selectedPaths.Contains(entry.Path)))
                _list.SelectedItems.Add(entry);
            _selectCurrent.IsEnabled = fullPath is not null;
            _selectCurrent.IsChecked = fullPath is not null && _selectedPaths.Contains(fullPath);
            _refreshingSelection = false;
            UpdateStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (navigationVersion != _navigationVersion)
                return;
            _status.Text = T("Klasör açılamadı: ", "Could not open the folder: ") + ex.Message;
        }
        finally
        {
            _refreshingSelection = false;
        }
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingSelection)
            return;
        foreach (var entry in e.AddedItems.OfType<FolderEntry>())
            _selectedPaths.Add(entry.Path);
        foreach (var entry in e.RemovedItems.OfType<FolderEntry>())
            _selectedPaths.Remove(entry.Path);
        UpdateStatus();
    }

    private void SetCurrentPathSelected(bool selected)
    {
        if (_refreshingSelection || _currentPath is null)
            return;
        if (selected)
            _selectedPaths.Add(_currentPath);
        else
            _selectedPaths.Remove(_currentPath);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _confirmButton.IsEnabled = _selectedPaths.Count > 0;
        _status.Text = T(
            $"{_entries.Count:N0} alt klasör • {_selectedPaths.Count:N0} seçim • Sahiplik ve izinler değiştirilmez",
            $"{_entries.Count:N0} subfolders • {_selectedPaths.Count:N0} selected • Ownership and permissions are not changed");
    }

    private string T(string turkish, string english) => _english ? english : turkish;

    private sealed record FolderEntry(string Path, string Label);
}
