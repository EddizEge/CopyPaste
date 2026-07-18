using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace CopyPaste.App;

public sealed class ProtectedFolderPickerWindow : Window
{
    private readonly string _resultFile;
    private readonly ObservableCollection<FolderEntry> _entries = [];
    private readonly ListView _list = new();
    private readonly TextBox _pathBox = new();
    private readonly TextBlock _status = new();
    private string? _currentPath;

    public ProtectedFolderPickerWindow(string resultFile)
    {
        _resultFile = resultFile;
        Title = "CopyPaste — Korumalı kaynak seç";
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        AppWindow.GetFromWindowId(windowId).Resize(new Windows.Graphics.SizeInt32(760, 620));

        _pathBox.PlaceholderText = "Klasör yolunu yazabilir veya listeden gezebilirsiniz";
        var goButton = new Button { Content = "Git" };
        goButton.Click += (_, _) => Navigate(_pathBox.Text);
        var upButton = new Button { Content = "↑ Üst klasör" };
        upButton.Click += (_, _) => Navigate(_currentPath is null ? null : Directory.GetParent(_currentPath)?.FullName);
        var pathRow = new Grid { ColumnSpacing = 8 };
        pathRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        pathRow.Children.Add(_pathBox);
        Grid.SetColumn(goButton, 1);
        pathRow.Children.Add(goButton);
        Grid.SetColumn(upButton, 2);
        pathRow.Children.Add(upButton);

        _list.ItemsSource = _entries;
        _list.DisplayMemberPath = nameof(FolderEntry.Label);
        _list.SelectionMode = ListViewSelectionMode.Single;
        _list.DoubleTapped += (_, _) =>
        {
            if (_list.SelectedItem is FolderEntry selected)
                Navigate(selected.Path);
        };
        var selectButton = new Button { Content = "Bu klasörü seç", HorizontalAlignment = HorizontalAlignment.Right };
        selectButton.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_currentPath))
                return;
            await File.WriteAllTextAsync(_resultFile, _currentPath);
            Close();
        };
        var cancelButton = new Button { Content = "İptal" };
        cancelButton.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(selectButton);

        var panel = new Grid { Padding = new Thickness(20), RowSpacing = 12 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.Children.Add(pathRow);
        Grid.SetRow(_list, 1);
        panel.Children.Add(_list);
        Grid.SetRow(_status, 2);
        panel.Children.Add(_status);
        Grid.SetRow(buttons, 3);
        panel.Children.Add(buttons);
        Content = panel;
        Navigate(null);
    }

    private void Navigate(string? path)
    {
        _entries.Clear();
        _status.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            _currentPath = null;
            _pathBox.Text = string.Empty;
            foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
                _entries.Add(new(drive.RootDirectory.FullName, $"{drive.Name}  {drive.VolumeLabel}"));
            return;
        }
        try
        {
            var fullPath = Path.GetFullPath(path.Trim());
            _currentPath = fullPath;
            _pathBox.Text = fullPath;
            foreach (var directory in Directory.EnumerateDirectories(fullPath)
                         .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
            {
                _entries.Add(new(directory, Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))));
            }
            _status.Text = $"{_entries.Count:N0} alt klasör • Sahiplik ve izinler değiştirilmez";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = "Klasör açılamadı: " + ex.Message + " Yol biliniyorsa yukarıya yazıp doğrudan seçebilirsiniz.";
        }
    }

    private sealed record FolderEntry(string Path, string Label);
}
