using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CopyPaste.App.Services;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CopyPaste.App;

public sealed class DeletionToolWindow : Window
{
    private readonly DeletionToolView _view;

    public DeletionToolWindow(bool english)
    {
        Title = DeletionElevationService.IsElevated()
            ? english ? "CopyPaste — Controlled deletion (Administrator)" : "CopyPaste — Kontrollü silme (Yönetici)"
            : english ? "CopyPaste — Controlled deletion" : "CopyPaste — Kontrollü silme";
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(920, 760));
        TitleBarThemeService.AttachStandard(this, appWindow);
        _view = new DeletionToolView(english, this, Close);
        appWindow.Closing += (_, args) => args.Cancel = !_view.RequestNavigateAway();
        Content = _view;
    }
}

public sealed class DeletionToolView : UserControl
{
    private readonly bool _english;
    private readonly Window _owner;
    private readonly Action? _closeRequested;
    private readonly ObservableCollection<string> _paths = [];
    private readonly DeletionSelectionState _selection = new();
    private readonly DeletionPlanner _planner = new();
    private readonly DeletionHistoryStore _historyStore = new();
    private readonly Grid _root = new() { Background = Brush("PageBrush") };
    private readonly ListView _pathList = new() { SelectionMode = ListViewSelectionMode.Multiple };
    private readonly TextBox _manualPath = new();
    private readonly ComboBox _mode = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
    private readonly Button _previewButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _elevateButton = new();
    private DeletionPlan? _plan;
    private long _planSelectionVersion;
    private bool _busy;
    private CancellationTokenSource? _operationCancellation;

    public DeletionToolView(bool english, Window owner, Action? closeRequested = null)
    {
        _english = english;
        _owner = owner;
        _closeRequested = closeRequested;
        _root.RequestedTheme = ElementTheme.Dark;

        _pathList.ItemsSource = _paths;
        _pathList.MinHeight = 210;
        _pathList.SelectionChanged += (_, _) => UpdateRemoveButton();
        _manualPath.PlaceholderText = T("Tam dosya veya klasör yolu", "Full file or folder path");
        _manualPath.TextChanged += (_, _) => UpdateAddPathButton();

        _mode.ItemsSource = new[]
        {
            new ModeChoice(DeletionMode.RecycleBin,
                T("Geri Dönüşüm Kutusu — önerilen", "Recycle Bin — recommended")),
            new ModeChoice(DeletionMode.Permanent,
                T("Kalıcı silme — yönetici izni isteyebilir", "Permanent delete — may request administrator approval")),
            new ModeChoice(DeletionMode.SecureErase,
                T("Best-effort güvenli silme — gelişmiş", "Best-effort secure erase — advanced"))
        };
        _mode.DisplayMemberPath = nameof(ModeChoice.Label);
        _mode.SelectedIndex = 0;
        _mode.SelectionChanged += (_, _) => InvalidatePreview();

        _previewButton.Content = T("Hedefleri önizle", "Preview targets");
        _previewButton.Style = GetStyle("QuietButtonStyle");
        _previewButton.Click += async (_, _) => await PreviewAsync();
        _deleteButton.Content = T("Silme işlemini başlat", "Start deletion");
        _deleteButton.Style = GetStyle("PrimaryButtonStyle");
        _deleteButton.IsEnabled = false;
        _deleteButton.Click += async (_, _) => await DeleteAsync();
        _cancelButton.Content = T("İşlemi iptal et", "Cancel operation");
        _cancelButton.Style = GetStyle("QuietButtonStyle");
        _cancelButton.Visibility = Visibility.Collapsed;
        _cancelButton.Click += (_, _) => _operationCancellation?.Cancel();

        _root.Children.Add(BuildContent());
        Content = _root;
        UpdateRemoveButton();
        ShowIntro();
    }

    public bool RequestNavigateAway()
    {
        if (_operationCancellation is null)
            return true;
        _operationCancellation.Cancel();
        _summary.Text = T("İptal istendi; geçerli güvenli noktaya ulaşılması bekleniyor…",
            "Cancellation requested; waiting for the next safe stopping point…");
        return false;
    }

    private readonly Button _addPathButton = new();
    private readonly Button _removeButton = new();
    private readonly Button _selectFilesButton = new();
    private readonly Button _selectFolderButton = new();
    private readonly Button _clearButton = new();

    private UIElement BuildContent()
    {
        var title = new TextBlock
        {
            Text = T("Kontrollü dosya ve klasör silme", "Controlled file and folder deletion"),
            Style = GetStyle("SectionTitleStyle"),
            FontSize = 24
        };
        var subtitle = new TextBlock
        {
            Text = T(
                "Bu araç kopyalama ve eşitlemeden tamamen ayrıdır. Yalnızca burada açıkça seçtiğiniz hedeflere dokunur.",
                "This tool is completely separate from copying and synchronization. It touches only targets explicitly selected here."),
            Style = GetStyle("MutedTextStyle"),
            TextWrapping = TextWrapping.Wrap
        };

        _selectFilesButton.Content = T("Dosyaları seç", "Select files");
        _selectFilesButton.Style = GetStyle("QuietButtonStyle");
        _selectFilesButton.Click += SelectFiles_Click;
        _selectFolderButton.Content = T("Klasör seç", "Select folder");
        _selectFolderButton.Style = GetStyle("QuietButtonStyle");
        _selectFolderButton.Click += SelectFolder_Click;
        _elevateButton.Content = T("Yönetici olarak yeniden aç", "Reopen as administrator");
        _elevateButton.Style = GetStyle("QuietButtonStyle");
        _elevateButton.Visibility = DeletionElevationService.IsElevated()
            ? Visibility.Collapsed
            : Visibility.Visible;
        _elevateButton.Click += (_, _) =>
        {
            if (DeletionElevationService.LaunchElevatedTool(_english ? "en-US" : "tr-TR"))
                _closeRequested?.Invoke();
            else
            {
                _summary.Text = T("Windows yönetici onayı verilmedi; hiçbir hedef değiştirilmedi.",
                    "Windows administrator approval was not granted; no targets were changed.");
            }
        };
        _addPathButton.Content = T("Yolu ekle", "Add path");
        _addPathButton.Style = GetStyle("QuietButtonStyle");
        _addPathButton.IsEnabled = false;
        _addPathButton.Click += (_, _) => AddPath(_manualPath.Text);
        _removeButton.Content = T("Seçilenleri kaldır", "Remove selected");
        _removeButton.Style = GetStyle("QuietButtonStyle");
        _removeButton.Click += (_, _) => RemoveSelectedPaths();
        _clearButton.Content = T("Listeyi temizle", "Clear list");
        _clearButton.Style = GetStyle("QuietButtonStyle");
        _clearButton.Click += (_, _) => ClearPaths();

        var manualRow = new Grid { ColumnSpacing = 8 };
        manualRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        manualRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        manualRow.Children.Add(_manualPath);
        Grid.SetColumn(_addPathButton, 1);
        manualRow.Children.Add(_addPathButton);

        var pickerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        pickerButtons.Children.Add(_selectFilesButton);
        pickerButtons.Children.Add(_selectFolderButton);
        pickerButtons.Children.Add(_removeButton);
        pickerButtons.Children.Add(_clearButton);
        pickerButtons.Children.Add(_elevateButton);

        var targetsPanel = new StackPanel { Spacing = 10 };
        targetsPanel.Children.Add(new TextBlock
        {
            Text = T("1. Silinecek hedefleri seçin", "1. Select deletion targets"),
            Style = GetStyle("SectionTitleStyle")
        });
        targetsPanel.Children.Add(manualRow);
        targetsPanel.Children.Add(pickerButtons);
        targetsPanel.Children.Add(_pathList);
        var targetsCard = Card(targetsPanel);

        var modePanel = new StackPanel { Spacing = 10 };
        modePanel.Children.Add(new TextBlock
        {
            Text = T("2. Silme yöntemini seçin", "2. Choose a deletion method"),
            Style = GetStyle("SectionTitleStyle")
        });
        modePanel.Children.Add(_mode);
        modePanel.Children.Add(new TextBlock
        {
            Text = T(
                "Best-effort güvenli silme yalnızca yerel sabit NTFS dosyalarında çalışır. SSD/TRIM, yedek, snapshot, bulut ve NAS kopyaları için geri getirilemezlik garantisi yoktur.",
                "Best-effort secure erase works only for files on local fixed NTFS volumes. There is no unrecoverability guarantee for SSD/TRIM, backups, snapshots, cloud copies, or NAS storage."),
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 190, 117)),
            TextWrapping = TextWrapping.Wrap
        });
        modePanel.Children.Add(_previewButton);
        var modeCard = Card(modePanel);

        _summary.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        _details.Foreground = Brush("MutedBrush");
        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        actionButtons.Children.Add(_cancelButton);
        actionButtons.Children.Add(_deleteButton);
        var resultPanel = new StackPanel { Spacing = 10 };
        resultPanel.Children.Add(new TextBlock
        {
            Text = T("3. Önizlemeyi doğrulayın", "3. Verify the preview"),
            Style = GetStyle("SectionTitleStyle")
        });
        resultPanel.Children.Add(_summary);
        resultPanel.Children.Add(_details);
        resultPanel.Children.Add(_progress);
        resultPanel.Children.Add(actionButtons);
        var resultCard = Card(resultPanel);

        var content = new StackPanel { Spacing = 14, Padding = new Thickness(22) };
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(targetsCard);
        content.Children.Add(modeCard);
        content.Children.Add(resultCard);
        return new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private async void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
            foreach (var file in await picker.PickMultipleFilesAsync())
                AddPath(file.Path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException
            or COMException)
        {
            _summary.Text = T(
                "Windows dosya seçiciyi açamadı. Tam yolu yukarıdaki alana yazabilir veya yapıştırabilirsiniz.",
                "Windows could not open the file picker. Type or paste the full path in the field above.");
            _details.Text = ex.Message;
        }
    }

    private async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
                AddPath(folder.Path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException
            or COMException)
        {
            _summary.Text = T(
                "Windows klasör seçiciyi açamadı. Tam yolu yukarıdaki alana yazabilir veya yapıştırabilirsiniz.",
                "Windows could not open the folder picker. Type or paste the full path in the field above.");
            _details.Text = ex.Message;
        }
    }

    // Every list mutation goes through the selection state so its version invalidates older previews.
    private void AddPath(string? path)
    {
        if (_busy || string.IsNullOrWhiteSpace(path))
            return;
        if (_selection.TryAdd(path, out var candidate))
            _paths.Add(candidate);
        _manualPath.Text = string.Empty;
        InvalidatePreview();
    }

    private void RemoveSelectedPaths()
    {
        if (_busy)
            return;
        foreach (var path in _pathList.SelectedItems.Cast<string>().ToArray())
        {
            if (_selection.Remove(path))
                _paths.Remove(path);
        }
        InvalidatePreview();
    }

    private void ClearPaths()
    {
        if (_busy)
            return;
        _selection.Clear();
        _paths.Clear();
        InvalidatePreview();
    }

    private async Task PreviewAsync()
    {
        if (_busy)
            return;
        if (_selection.Count == 0 || _mode.SelectedItem is not ModeChoice choice)
        {
            _summary.Text = T("Önce en az bir hedef seçin.", "Select at least one target first.");
            return;
        }
        var snapshot = _selection.Snapshot();
        SetBusy(true, T("Hedefler güvenli biçimde taranıyor…", "Targets are being scanned safely…"));
        try
        {
            var plan = await _planner.CreateAsync(snapshot.Paths, choice.Mode);
            if (!_selection.IsCurrent(snapshot.Version))
            {
                InvalidatePreview();
                ShowStaleSelection();
                return;
            }
            _plan = plan;
            _planSelectionVersion = snapshot.Version;
            _summary.Text = T(
                $"{_plan.FileCount:N0} dosya • {_plan.DirectoryCount:N0} klasör/bağlantı • {FormatBytes(_plan.TotalBytes)}",
                $"{_plan.FileCount:N0} files • {_plan.DirectoryCount:N0} folders/links • {FormatBytes(_plan.TotalBytes)}");
            if (_plan.Issues.Count > 0)
            {
                _details.Text = T("İşlem engellendi:\n", "Operation blocked:\n")
                    + string.Join("\n", _plan.Issues.Take(12).Select(issue => $"• {issue.Path}\n  {LocalizeIssue(issue)}"));
            }
            else
            {
                _details.Text = string.Join("\n", _plan.Warnings.Select(warning => "• " + LocalizeWarning(warning)));
            }
            _deleteButton.IsEnabled = _plan.IsValid;
        }
        catch (OperationCanceledException)
        {
            _summary.Text = T("Önizleme iptal edildi.", "Preview canceled.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DeleteAsync()
    {
        if (_busy || _plan is not { IsValid: true } plan)
            return;
        if (!IsPlanCurrent(plan))
        {
            InvalidatePreview();
            ShowStaleSelection();
            return;
        }
        if (!await ConfirmAsync(plan))
            return;
        if (!IsPlanCurrent(plan))
        {
            InvalidatePreview();
            ShowStaleSelection();
            return;
        }

        SetBusy(true, T("Silme işlemi başlatılıyor…", "Starting deletion…"), allowCancel: true);
        _operationCancellation = new CancellationTokenSource();
        try
        {
            var progress = new Progress<DeletionProgress>(value =>
            {
                var percent = value.TotalBytes > 0
                    ? Math.Clamp(value.ProcessedBytes * 100d / value.TotalBytes, 0, 100)
                    : value.TotalItems == 0 ? 0 : value.CompletedItems * 100d / value.TotalItems;
                _progress.IsIndeterminate = false;
                _progress.Value = percent;
                _summary.Text = value.TotalPasses > 0
                    ? T($"Üzerine yazılıyor: geçiş {value.Pass}/{value.TotalPasses}",
                        $"Overwriting: pass {value.Pass}/{value.TotalPasses}")
                    : T($"Siliniyor: {value.CompletedItems}/{value.TotalItems}",
                        $"Deleting: {value.CompletedItems}/{value.TotalItems}");
                _details.Text = value.Path;
            });
            DeletionResult result;
            if (plan.Mode == DeletionMode.RecycleBin || DeletionElevationService.IsElevated())
                result = await new DeletionService(_planner).ExecuteAsync(
                    plan, progress, _operationCancellation.Token);
            else
            {
                _progress.IsIndeterminate = true;
                _details.Text = T(
                    "Windows yönetici onayını bekliyor. Onaydan sonra işlem ayrıcalıklı süreçte devam eder.",
                    "Windows is waiting for administrator approval. After approval, the operation continues in an elevated process.");
                result = await DeletionElevationService.ExecuteAsync(plan, _operationCancellation.Token);
            }
            ShowResult(result);
            try
            {
                await _historyStore.AddAsync(new DeletionHistoryEntry(
                    Guid.NewGuid(), DateTimeOffset.Now, result.Mode,
                    plan.RequestedPaths.Count, result.SucceededCount, result.FailedCount,
                    plan.TotalBytes, result.WasCanceled));
            }
            catch (IOException)
            {
                _details.Text += T("\nYerel silme özeti kaydedilemedi.",
                    "\nThe local deletion summary could not be saved.");
            }
            _plan = null;
            _deleteButton.IsEnabled = false;
            if (result.SucceededCount > 0)
            {
                foreach (var succeeded in result.Items.Where(item => item.Succeeded)
                             .Select(item => item.Path).ToArray())
                {
                    if (_selection.Remove(succeeded))
                        _paths.Remove(succeeded);
                }
            }
        }
        catch (ElevatedDeletionException ex)
        {
            _summary.Text = ex.Kind switch
            {
                ElevatedDeletionFailureKind.RequestRejected => T(
                    "Yönetici işlemi isteği doğrulayamadı ve reddetti; hiçbir öğe silinmedi.",
                    "The administrator process could not validate the request and rejected it; no items were deleted."),
                ElevatedDeletionFailureKind.BrokerFailed => T(
                    "Yönetici işlemi silme sırasında beklenmeyen bir hatayla durdu; bazı öğeler işlenmiş olabilir. Yeniden önizleyin.",
                    "The administrator process stopped with an unexpected error during deletion; some items may have been processed. Preview again."),
                _ => T(
                    "Yönetici işlemi okunabilir bir sonuç döndürmedi; hedeflerin durumunu yeniden önizleyerek denetleyin.",
                    "The administrator process did not return a readable result; preview again to check the targets.")
            };
            _details.Text = ex.Message;
            _plan = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidOperationException)
        {
            _summary.Text = T("Silme işlemi tamamlanamadı.", "The deletion operation could not be completed.");
            _details.Text = ex.Message;
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private async Task<bool> ConfirmAsync(DeletionPlan plan)
    {
        var irreversible = plan.Mode != DeletionMode.RecycleBin;
        var phrase = T("KALICI OLARAK SİL", "DELETE PERMANENTLY");
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = irreversible
                ? T(
                    $"Bu işlem {plan.FileCount:N0} dosya ve {plan.DirectoryCount:N0} klasör/bağlantıyı Geri Dönüşüm Kutusu'na göndermeden değiştirecek veya silecek.",
                    $"This operation will change or delete {plan.FileCount:N0} files and {plan.DirectoryCount:N0} folders/links without using the Recycle Bin.")
                : T(
                    $"{plan.FileCount + plan.DirectoryCount:N0} öğe Geri Dönüşüm Kutusu'na taşınacak.",
                    $"{plan.FileCount + plan.DirectoryCount:N0} items will be moved to the Recycle Bin."),
            TextWrapping = TextWrapping.Wrap
        });
        TextBox? confirmation = null;
        if (irreversible)
        {
            panel.Children.Add(new TextBlock
            {
                Text = T($"Devam etmek için tam olarak “{phrase}” yazın.",
                    $"Type “{phrase}” exactly to continue."),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange),
                TextWrapping = TextWrapping.Wrap
            });
            confirmation = new TextBox { PlaceholderText = phrase };
            panel.Children.Add(confirmation);
        }
        var dialog = new ContentDialog
        {
            XamlRoot = _root.XamlRoot,
            Title = irreversible
                ? T("Geri döndürülemez işlemi doğrulayın", "Confirm irreversible operation")
                : T("Silme işlemini doğrulayın", "Confirm deletion"),
            Content = panel,
            PrimaryButtonText = irreversible ? T("Kalıcı sil", "Delete permanently") : T("Taşı", "Move"),
            CloseButtonText = T("Vazgeç", "Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return false;
        if (!irreversible || confirmation?.Text == phrase)
            return true;
        _summary.Text = T("Onay ifadesi eşleşmedi; hiçbir öğe değiştirilmedi.",
            "The confirmation phrase did not match; no items were changed.");
        return false;
    }

    private void ShowResult(DeletionResult result)
    {
        _summary.Text = result.WasCanceled
            ? T($"İşlem iptal edildi • {result.SucceededCount} başarılı • {result.FailedCount} başarısız",
                $"Operation canceled • {result.SucceededCount} succeeded • {result.FailedCount} failed")
            : T($"İşlem tamamlandı • {result.SucceededCount} başarılı • {result.FailedCount} başarısız",
                $"Operation completed • {result.SucceededCount} succeeded • {result.FailedCount} failed");
        var failed = result.Items.Where(item => !item.Succeeded).Take(12).ToArray();
        _details.Text = failed.Length == 0
            ? T("Seçilen hedefler işlendi.", "The selected targets were processed.")
            : string.Join("\n", failed.Select(item =>
                $"• {item.Path}\n  {item.ErrorMessage}"
                + (item.PartiallyOverwritten
                    ? T("\n  Uyarı: dosya kısmen veya tamamen üzerine yazılmış olabilir.",
                        "\n  Warning: the file may have been partially or fully overwritten.")
                    : string.Empty)));
    }

    private void SetBusy(bool busy, string? message = null, bool allowCancel = false)
    {
        _busy = busy;
        _previewButton.IsEnabled = !busy;
        _deleteButton.IsEnabled = !busy && _plan is { } plan && IsPlanCurrent(plan);
        _mode.IsEnabled = !busy;
        _pathList.IsEnabled = !busy;
        _manualPath.IsEnabled = !busy;
        _selectFilesButton.IsEnabled = !busy;
        _selectFolderButton.IsEnabled = !busy;
        _clearButton.IsEnabled = !busy;
        _elevateButton.IsEnabled = !busy;
        UpdateAddPathButton();
        UpdateRemoveButton();
        _cancelButton.Visibility = busy && allowCancel ? Visibility.Visible : Visibility.Collapsed;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _progress.IsIndeterminate = busy;
        if (message is not null)
            _summary.Text = message;
    }

    private void InvalidatePreview()
    {
        _plan = null;
        _deleteButton.IsEnabled = false;
        ShowIntro();
        UpdateRemoveButton();
    }

    private void ShowIntro()
    {
        _summary.Text = T("Henüz doğrulanmış bir silme önizlemesi yok.",
            "There is no validated deletion preview yet.");
        _details.Text = T(
            "Hedefleri ve yöntemi seçtikten sonra önizleme oluşturun. Dosyalar önizleme ile işlem arasında yeniden doğrulanır.",
            "Create a preview after selecting targets and a method. Files are revalidated between preview and execution.");
    }

    private bool IsPlanCurrent(DeletionPlan plan) =>
        plan.IsValid && _selection.IsCurrent(_planSelectionVersion);

    private void ShowStaleSelection()
    {
        _summary.Text = T("Seçim önizleme sırasında değişti; yeniden önizleyin.",
            "The selection changed during the preview; preview again.");
        _details.Text = T("Eski önizleme kullanılmadı; hiçbir öğe değiştirilmedi.",
            "The outdated preview was discarded; no items were changed.");
    }

    private void UpdateAddPathButton() =>
        _addPathButton.IsEnabled = !_busy && !string.IsNullOrWhiteSpace(_manualPath.Text);

    private void UpdateRemoveButton() =>
        _removeButton.IsEnabled = !_busy && _pathList.SelectedItems.Count > 0;

    private string LocalizeIssue(DeletionIssue issue) => issue.Code switch
    {
        DeletionIssueCode.VolumeRoot => T("Disk/birim kökü silinemez.", "A disk/volume root cannot be deleted."),
        DeletionIssueCode.ProtectedPath => T("Bu yol CopyPaste güvenlik sınırlarıyla korunuyor.",
            "This path is protected by CopyPaste safety boundaries."),
        DeletionIssueCode.UnsupportedStorage => T("Seçilen yöntem bu depolama türünde desteklenmiyor.",
            "The selected method is not supported on this storage type."),
        DeletionIssueCode.ReparsePoint => T("Güvenli silme reparse point hedeflerini izlemez veya üzerine yazmaz.",
            "Secure erase does not follow or overwrite reparse-point targets."),
        DeletionIssueCode.MultipleHardLinks => T("Dosyanın birden fazla hard link'i var; başka bir yolu değiştirmemek için işlem engellendi.",
            "The file has multiple hard links; the operation was blocked to avoid changing another path."),
        DeletionIssueCode.AccessDenied => T("Windows erişimi reddetti; sahiplik ve ACL değiştirilmeyecek.",
            "Windows denied access; ownership and ACLs will not be changed."),
        DeletionIssueCode.Missing => T("Hedef artık mevcut değil.", "The target no longer exists."),
        DeletionIssueCode.HardLinkCountUnavailable => T(
            "Dosyanın hard link sayısı okunamadı; başka bir yolu değiştirmemek için güvenli silme engellendi.",
            "The file's hard link count could not be read; secure erase was blocked to avoid changing another path."),
        _ => issue.Message
    };

    private string LocalizeWarning(string warning) => _plan?.Mode switch
    {
        DeletionMode.RecycleBin => T(
            "Öğeler, kutu boşaltılana kadar Geri Dönüşüm Kutusu'ndan kurtarılabilir.",
            "Items can be recovered from the Recycle Bin until it is emptied."),
        DeletionMode.Permanent => T(
            "Kalıcı silme Geri Dönüşüm Kutusu'nu atlar ve CopyPaste tarafından geri alınamaz.",
            "Permanent deletion bypasses the Recycle Bin and cannot be undone by CopyPaste."),
        DeletionMode.SecureErase when warning.StartsWith("Secure erasure overwrites", StringComparison.Ordinal) => T(
            "Güvenli silme dosya içeriğini silmeden önce üzerine yazar; dosya değişmeye başladıktan sonra iptal güvenli değildir.",
            warning),
        DeletionMode.SecureErase => T(
            "Kopyalar, yedekler, snapshot'lar, SSD wear-leveling, TRIM, önbellekler ve yeniden eşlenen sektörler veri bırakabilir; evrensel garanti mümkün değildir.",
            warning),
        _ => warning
    };

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    private static Border Card(UIElement child) => new()
    {
        Style = GetStyle("CardStyle"),
        Child = child
    };

    private static Style GetStyle(string key)
    {
        try
        {
            if (Application.Current.Resources[key] is Style style)
                return style;
        }
        catch (COMException)
        {
            // Standalone/elevated startup can reach this window before App.xaml resources are materialized.
        }

        var fallback = key switch
        {
            "CardStyle" => new Style(typeof(Border)),
            "SectionTitleStyle" or "MutedTextStyle" => new Style(typeof(TextBlock)),
            _ => new Style(typeof(Button))
        };
        if (key == "CardStyle")
        {
            fallback.Setters.Add(new Setter(Border.BackgroundProperty, ColorBrush(0x14, 0x17, 0x1D)));
            fallback.Setters.Add(new Setter(Border.BorderBrushProperty, ColorBrush(0x30, 0x35, 0x41)));
            fallback.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
            fallback.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(14)));
            fallback.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(18)));
        }
        else if (key == "PrimaryButtonStyle")
        {
            fallback.Setters.Add(new Setter(Control.BackgroundProperty, ColorBrush(0x6D, 0x5D, 0xFB)));
            fallback.Setters.Add(new Setter(Control.ForegroundProperty, ColorBrush(0xFF, 0xFF, 0xFF)));
            fallback.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            fallback.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(7)));
            fallback.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 10, 18, 10)));
            fallback.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 40d));
        }
        else if (key == "QuietButtonStyle")
        {
            fallback.Setters.Add(new Setter(Control.BackgroundProperty, ColorBrush(0x20, 0x24, 0x2C)));
            fallback.Setters.Add(new Setter(Control.ForegroundProperty, ColorBrush(0xE8, 0xEA, 0xF0)));
            fallback.Setters.Add(new Setter(Control.BorderBrushProperty, ColorBrush(0x37, 0x3C, 0x48)));
            fallback.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            fallback.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(7)));
            fallback.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 9, 14, 9)));
            fallback.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 38d));
        }
        else if (key == "SectionTitleStyle")
        {
            fallback.Setters.Add(new Setter(TextBlock.ForegroundProperty, ColorBrush(0xFF, 0xFF, 0xFF)));
            fallback.Setters.Add(new Setter(TextBlock.FontSizeProperty, 18d));
        }
        else if (key == "MutedTextStyle")
        {
            fallback.Setters.Add(new Setter(TextBlock.ForegroundProperty, ColorBrush(0x9D, 0xA5, 0xB5)));
            fallback.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12d));
        }
        return fallback;
    }

    private static Brush Brush(string key)
    {
        try
        {
            if (Application.Current.Resources[key] is Brush brush)
                return brush;
        }
        catch (COMException)
        {
            // See Style: keep the standalone deletion window usable during early startup.
        }
        return key == "MutedBrush"
            ? ColorBrush(0x9D, 0xA5, 0xB5)
            : ColorBrush(0x0B, 0x0C, 0x10);
    }

    private static SolidColorBrush ColorBrush(byte red, byte green, byte blue) =>
        new(Microsoft.UI.ColorHelper.FromArgb(255, red, green, blue));
    private string T(string turkish, string english) => _english ? english : turkish;
    private sealed record ModeChoice(DeletionMode Mode, string Label);
}
