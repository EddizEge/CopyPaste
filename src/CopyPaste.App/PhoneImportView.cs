using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using CopyPaste.App.Services;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CopyPaste.App;

/// <summary>
/// "Import from phone" workspace. Phones are listed through the WinRT backend with the shell backend as fallback;
/// the user picks a device folder (DCIM by default) and a local destination, and the Core engine copies it. The
/// device is only read: nothing on the phone is deleted, changed, or renamed.
/// </summary>
public sealed class PhoneImportView : UserControl, IDisposable
{
    private const double WideLayoutWidth = 900;
    private const double NarrowButtonsWidth = 640;

    private readonly bool _english;
    private readonly CultureInfo _culture;
    private readonly Window _owner;
    private readonly WinRtPortableDeviceSource _winRt = new();
    private readonly ShellPortableDeviceSource _shell = new();
    private readonly PhoneImportEngine _engine = new();
    private readonly PhoneImportDiagnostics _diagnostics = new();
    private readonly ObservableCollection<DeviceChoice> _deviceChoices = [];
    private readonly ObservableCollection<FolderChoice> _folderChoices = [];

    private readonly Grid _topRow = new() { ColumnSpacing = 14, RowSpacing = 14 };
    private readonly Border _deviceCard;
    private readonly Border _folderCard;
    private readonly ListView _deviceList = new() { SelectionMode = ListViewSelectionMode.Single, MinHeight = 120, MaxHeight = 240 };
    private readonly Button _refreshButton = new();
    private readonly ProgressRing _deviceRing = new() { Width = 18, Height = 18, IsActive = false, Visibility = Visibility.Collapsed };
    private readonly ComboBox _backendBox = new() { MinWidth = 230 };
    private readonly Button _pickFolderButton = new();
    private readonly TextBlock _deviceStatus = Muted();
    private readonly TextBlock _guidanceText = NoticeText();
    private readonly Border _guidanceBox;
    private readonly Button _upButton = new();
    private readonly TextBlock _breadcrumb = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly ListView _folderList = new() { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = true, MinHeight = 120, MaxHeight = 240 };
    private readonly TextBlock _folderStatus = Muted();
    private readonly TextBox _destinationBox = new();
    private readonly Button _browseButton = new();
    private readonly StackPanel _planButtons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _scanButton = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _summaryText = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly TextBlock _summaryWarningText = NoticeText();
    private readonly Border _summaryWarning;
    private readonly ProgressBar _progressBar = new() { Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed };
    private readonly TextBlock _progressText = Muted();
    private readonly TextBlock _resultText = NoticeText();
    private readonly Border _resultBox;
    private readonly TextBlock _failureText = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontSize = 12 };
    private readonly StackPanel _resultButtons = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly Button _openDestinationButton = new() { Visibility = Visibility.Collapsed };
    private readonly Button _retryButton = new() { Visibility = Visibility.Collapsed };
    private readonly Button _copyDiagnosticsButton = new();
    private readonly TextBlock _diagnosticsStatus = Muted();

    private PortableDeviceCatalog? _catalog;
    private PortableDeviceDiscovery? _discovery;
    private PortableDeviceInfo? _pickedDevice;
    private PortableDeviceInfo? _activeDevice;
    private List<PortableDeviceEntry> _path = [];
    private PhoneImportPlan? _plan;
    private PhoneImportResult? _lastResult;
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _deviceOperation;
    private bool _busy;
    private bool _loadedOnce;
    private bool _suppressDeviceSelection;
    private bool _disposed;

    public PhoneImportView(bool english, Window owner)
    {
        _english = english;
        _culture = CultureInfo.GetCultureInfo(english ? "en-US" : "tr-TR");
        _owner = owner;
        _guidanceBox = NoticeBox(_guidanceText);
        _summaryWarning = NoticeBox(_summaryWarningText);
        _resultBox = NoticeBox(_resultText);
        _deviceCard = Card(BuildDeviceCard());
        _folderCard = Card(BuildFolderCard());

        var root = new Grid { Background = Brush("PageBrush"), RequestedTheme = ElementTheme.Dark };
        root.Children.Add(new ScrollViewer
        {
            Content = BuildContent(),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        Content = root;
        SizeChanged += (_, args) => ApplyLayout(args.NewSize.Width);
        Loaded += async (_, _) =>
        {
            if (_loadedOnce)
                return;
            _loadedOnce = true;
            await RefreshDevicesAsync();
        };
        ShowIdleState();
    }

    public bool IsBusy => _operation is not null;

    /// <summary>Cancels a running import and keeps the workspace open until it reaches a safe stopping point.</summary>
    public bool RequestNavigateAway()
    {
        if (_operation is null)
        {
            _deviceOperation?.Cancel();
            return true;
        }
        _operation.Cancel();
        _progressText.Text = T("İptal istendi; geçerli dosya güvenle bırakılıyor…",
            "Cancellation requested; the current file is being released safely…");
        return false;
    }

    /// <summary>Uses a folder from the Windows folder picker that has no file-system path (a phone folder) as the source.</summary>
    public void UsePickedFolder(StorageFolder folder)
    {
        if (_busy || !string.IsNullOrEmpty(folder.Path))
            return;
        _pickedDevice = _winRt.CreatePickedFolderDevice(folder);
        var choice = new DeviceChoice(_pickedDevice, PickedLabel(_pickedDevice), IsPicked: true);
        var existing = _deviceChoices.FirstOrDefault(item => item.IsPicked);
        if (existing is not null)
            _deviceChoices.Remove(existing);
        _deviceChoices.Insert(0, choice);
        _deviceList.SelectedItem = choice;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _deviceOperation?.Cancel();
        _operation?.Cancel();
        _shell.Dispose();
    }

    private UIElement BuildContent()
    {
        var content = new StackPanel { Spacing = 14, Padding = new Thickness(22), MaxWidth = 1240 };
        content.Children.Add(new TextBlock
        {
            Text = T("Telefondan aktar", "Import from phone"),
            Style = GetStyle("SectionTitleStyle"),
            FontSize = 24
        });
        content.Children.Add(new TextBlock
        {
            Text = T(
                "iPhone veya Android telefondaki fotoğraf ve videoları bu bilgisayardaki bir klasöre kopyalar. Telefon yalnızca okunur; telefonda hiçbir dosya silinmez, değiştirilmez veya yeniden adlandırılmaz.",
                "Copies photos and videos from an iPhone or Android phone to a folder on this PC. The phone is only read; no file on the phone is deleted, changed, or renamed."),
            Style = GetStyle("MutedTextStyle"),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = T(
                "✓ Var olan dosyaların üzerine yazılmaz     ✓ Her dosya SHA-256 ile doğrulanır     ✓ Yeniden başlatınca kaldığı yerden devam eder",
                "✓ Existing files are never overwritten     ✓ Every file is verified with SHA-256     ✓ Running it again resumes where it stopped"),
            Foreground = ColorBrush(0x8E, 0x96, 0xA6),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        _topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _topRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _topRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _topRow.Children.Add(_deviceCard);
        _topRow.Children.Add(_folderCard);
        ApplyLayout(WideLayoutWidth);
        content.Children.Add(_topRow);
        content.Children.Add(Card(BuildDestinationCard()));
        content.Children.Add(Card(BuildResultCard()));
        return content;
    }

    private UIElement BuildDeviceCard()
    {
        _refreshButton.Content = T("Yenile", "Refresh");
        _refreshButton.Style = GetStyle("QuietButtonStyle");
        _refreshButton.Click += async (_, _) => await RefreshDevicesAsync();
        AutomationProperties.SetName(_deviceRing, T("Telefonlar aranıyor", "Looking for phones"));

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(SectionTitle(T("1. Telefon", "1. Phone")));
        Grid.SetColumn(_deviceRing, 1);
        header.Children.Add(_deviceRing);
        Grid.SetColumn(_refreshButton, 2);
        header.Children.Add(_refreshButton);

        _deviceList.ItemsSource = _deviceChoices;
        _deviceList.DisplayMemberPath = nameof(DeviceChoice.Label);
        AutomationProperties.SetName(_deviceList, T("Bağlı telefonlar", "Connected phones"));
        _deviceList.SelectionChanged += async (_, _) =>
        {
            if (!_suppressDeviceSelection && _deviceList.SelectedItem is DeviceChoice choice)
                await OpenDeviceAsync(choice);
        };
        AutomationProperties.SetLiveSetting(_deviceStatus, AutomationLiveSetting.Polite);

        _backendBox.ItemsSource = new[]
        {
            new BackendChoice(BackendPreference.Automatic, T("Otomatik (önerilen)", "Automatic (recommended)")),
            new BackendChoice(BackendPreference.WinRt, T("Windows depolama (WinRT)", "Windows storage (WinRT)")),
            new BackendChoice(BackendPreference.Shell, T("Dosya Gezgini kabuğu", "File Explorer shell"))
        };
        _backendBox.DisplayMemberPath = nameof(BackendChoice.Label);
        _backendBox.SelectedIndex = 0;
        AutomationProperties.SetName(_backendBox, T("Erişim yolu", "Access path"));
        _backendBox.SelectionChanged += async (_, _) =>
        {
            if (_loadedOnce)
                await RefreshDevicesAsync();
        };

        _pickFolderButton.Content = T("Klasörü Windows seçicisiyle aç…", "Open a folder with the Windows picker…");
        _pickFolderButton.Style = GetStyle("QuietButtonStyle");
        ToolTipService.SetToolTip(_pickFolderButton, T(
            "Telefon listede görünmüyorsa Windows klasör seçicisinde Bu bilgisayar > telefon > Internal Storage > DCIM klasörünü seçin.",
            "If the phone isn't listed, choose This PC > phone > Internal Storage > DCIM in the Windows folder picker."));
        _pickFolderButton.Click += PickFolder_Click;

        var backendRow = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        backendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        backendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var backendLabel = Muted();
        backendLabel.Text = T("Erişim yolu", "Access path");
        backendLabel.VerticalAlignment = VerticalAlignment.Center;
        backendRow.Children.Add(backendLabel);
        Grid.SetColumn(_backendBox, 1);
        backendRow.Children.Add(_backendBox);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(header);
        panel.Children.Add(_deviceList);
        panel.Children.Add(_deviceStatus);
        panel.Children.Add(_guidanceBox);
        panel.Children.Add(backendRow);
        panel.Children.Add(_pickFolderButton);
        return panel;
    }

    private UIElement BuildFolderCard()
    {
        _upButton.Content = T("↑ Üst klasör", "↑ Parent folder");
        _upButton.Style = GetStyle("QuietButtonStyle");
        _upButton.IsEnabled = false;
        _upButton.Click += async (_, _) =>
        {
            if (_busy || _path.Count <= 1)
                return;
            _path.RemoveAt(_path.Count - 1);
            InvalidatePlan();
            await LoadFolderAsync(RestartDeviceOperation());
        };
        AutomationProperties.SetLiveSetting(_breadcrumb, AutomationLiveSetting.Polite);

        var breadcrumbRow = new Grid { ColumnSpacing = 10 };
        breadcrumbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        breadcrumbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        breadcrumbRow.Children.Add(_upButton);
        Grid.SetColumn(_breadcrumb, 1);
        breadcrumbRow.Children.Add(_breadcrumb);

        _folderList.ItemsSource = _folderChoices;
        _folderList.DisplayMemberPath = nameof(FolderChoice.Label);
        AutomationProperties.SetName(_folderList, T("Alt klasörler", "Subfolders"));
        _folderList.ItemClick += async (_, args) =>
        {
            if (_busy || args.ClickedItem is not FolderChoice choice)
                return;
            _path.Add(choice.Entry);
            InvalidatePlan();
            await LoadFolderAsync(RestartDeviceOperation());
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(SectionTitle(T("2. Telefondaki klasör", "2. Folder on the phone")));
        panel.Children.Add(breadcrumbRow);
        panel.Children.Add(_folderList);
        panel.Children.Add(_folderStatus);
        return panel;
    }

    private UIElement BuildDestinationCard()
    {
        _destinationBox.Style = GetStyle("PathTextBoxStyle");
        _destinationBox.PlaceholderText = T(@"Ör. D:\Fotoğraflar\iPhone", @"For example D:\Photos\iPhone");
        AutomationProperties.SetName(_destinationBox, T("Bu bilgisayardaki hedef klasör", "Destination folder on this PC"));
        _destinationBox.TextChanged += (_, _) => InvalidatePlan();
        _browseButton.Content = T("Gözat…", "Browse…");
        _browseButton.Style = GetStyle("QuietButtonStyle");
        _browseButton.Click += BrowseDestination_Click;

        var destinationRow = new Grid { ColumnSpacing = 8 };
        destinationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        destinationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        destinationRow.Children.Add(_destinationBox);
        Grid.SetColumn(_browseButton, 1);
        destinationRow.Children.Add(_browseButton);

        _scanButton.Content = T("Özeti hazırla", "Prepare summary");
        _scanButton.Style = GetStyle("QuietButtonStyle");
        _scanButton.Click += async (_, _) => await PrepareSummaryAsync();
        _startButton.Style = GetStyle("PrimaryButtonStyle");
        _startButton.Click += async (_, _) => await StartImportAsync();
        _cancelButton.Content = T("İptal", "Cancel");
        _cancelButton.Style = GetStyle("QuietButtonStyle");
        _cancelButton.Click += (_, _) => RequestNavigateAway();
        _planButtons.Children.Add(_cancelButton);
        _planButtons.Children.Add(_scanButton);
        _planButtons.Children.Add(_startButton);
        _summaryText.Foreground = ColorBrush(0xE8, 0xEA, 0xF0);
        AutomationProperties.SetLiveSetting(_summaryText, AutomationLiveSetting.Polite);

        var label = new TextBlock
        {
            Text = T("Bu bilgisayardaki hedef klasör", "Destination folder on this PC"),
            Foreground = ColorBrush(0xD8, 0xDB, 0xE4),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        var hint = Muted();
        hint.Text = T(
            "Seçili telefon klasörünün içeriği bu klasöre alt klasör yapısı korunarak kopyalanır. Hedefte aynı adla bulunan dosyalar atlanır.",
            "The contents of the selected phone folder are copied here with their subfolder structure. Files that already exist here with the same name are skipped.");

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(SectionTitle(T("3. Hedef ve özet", "3. Destination and summary")));
        panel.Children.Add(label);
        panel.Children.Add(destinationRow);
        panel.Children.Add(hint);
        panel.Children.Add(_summaryText);
        panel.Children.Add(_summaryWarning);
        panel.Children.Add(_planButtons);
        return panel;
    }

    private UIElement BuildResultCard()
    {
        AutomationProperties.SetName(_progressBar, T("Aktarma ilerlemesi", "Import progress"));
        AutomationProperties.SetLiveSetting(_resultText, AutomationLiveSetting.Polite);
        _failureText.Foreground = Brush("MutedBrush");
        _failureText.Visibility = Visibility.Collapsed;

        _openDestinationButton.Content = T("Hedef klasörü aç", "Open destination folder");
        _openDestinationButton.Style = GetStyle("QuietButtonStyle");
        _openDestinationButton.Click += (_, _) => OpenDestination();
        _retryButton.Content = T("Yeniden dene", "Retry");
        _retryButton.Style = GetStyle("QuietButtonStyle");
        ToolTipService.SetToolTip(_retryButton, T(
            "Klasörü yeniden tarar ve aktarır; hedefte zaten var olan dosyalar atlanır.",
            "Scans the folder again and imports it; files already at the destination are skipped."));
        _retryButton.Click += async (_, _) => await RetryAsync();
        _copyDiagnosticsButton.Content = T("Tanılama bilgisini kopyala", "Copy diagnostic information");
        _copyDiagnosticsButton.Style = GetStyle("QuietButtonStyle");
        ToolTipService.SetToolTip(_copyDiagnosticsButton, T(
            "Uygulama ve Windows sürümünü, görülen aygıtları ve hataları panoya kopyalar; hiçbir yere gönderilmez.",
            "Copies the app and Windows version, the devices seen, and the errors to the clipboard; nothing is sent anywhere."));
        _copyDiagnosticsButton.Click += (_, _) => CopyDiagnostics();
        _resultButtons.Children.Add(_openDestinationButton);
        _resultButtons.Children.Add(_retryButton);
        _resultButtons.Children.Add(_copyDiagnosticsButton);
        AutomationProperties.SetLiveSetting(_diagnosticsStatus, AutomationLiveSetting.Polite);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(SectionTitle(T("4. İlerleme ve sonuç", "4. Progress and result")));
        panel.Children.Add(_progressBar);
        panel.Children.Add(_progressText);
        panel.Children.Add(_resultBox);
        panel.Children.Add(_failureText);
        panel.Children.Add(_resultButtons);
        panel.Children.Add(_diagnosticsStatus);
        return panel;
    }

    private void ApplyLayout(double width)
    {
        var wide = width >= WideLayoutWidth;
        _topRow.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _topRow.ColumnSpacing = wide ? 14 : 0;
        Grid.SetColumn(_folderCard, wide ? 1 : 0);
        Grid.SetRow(_folderCard, wide ? 0 : 1);
        var narrow = width < NarrowButtonsWidth;
        foreach (var buttons in new[] { _planButtons, _resultButtons })
        {
            buttons.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
            buttons.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch
                : ReferenceEquals(buttons, _planButtons) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
    }

    private async Task RefreshDevicesAsync()
    {
        if (_busy || _disposed)
            return;
        var token = RestartDeviceOperation();
        SetDeviceLoading(true, T("Bağlı telefonlar aranıyor…", "Looking for connected phones…"));
        HideNotice(_guidanceBox);
        _suppressDeviceSelection = true;
        _deviceChoices.Clear();
        _suppressDeviceSelection = false;
        ClearFolderView();
        InvalidatePlan();
        try
        {
            _catalog = new PortableDeviceCatalog(SelectedSources());
            var discovery = await _catalog.DiscoverAsync(token);
            if (token.IsCancellationRequested)
                return;
            _discovery = discovery;
            _diagnostics.Discovery = discovery;
            foreach (var report in discovery.Reports.Where(report => report.ErrorHResult is not null))
                _diagnostics.RecordError($"{report.BackendName} {T("aygıt listesi", "device list")}",
                    report.ErrorHResult!.Value, report.ErrorMessage ?? string.Empty);

            _suppressDeviceSelection = true;
            if (_pickedDevice is not null)
                _deviceChoices.Add(new DeviceChoice(_pickedDevice, PickedLabel(_pickedDevice), IsPicked: true));
            foreach (var device in discovery.Devices)
                _deviceChoices.Add(new DeviceChoice(device, DeviceLabel(device), IsPicked: false));
            _suppressDeviceSelection = false;

            if (_deviceChoices.Count == 0)
            {
                var failedBackends = discovery.Reports.Where(report => report.ErrorHResult is not null)
                    .Select(report => $"{BackendLabel(report.BackendName)} {PhoneImportErrors.FormatHResult(report.ErrorHResult!.Value)}")
                    .ToArray();
                SetDeviceStatus(T("Bağlı telefon bulunamadı.", "No connected phone was found.")
                                + (failedBackends.Length > 0
                                    ? " " + T("Erişim hatası: ", "Access error: ") + string.Join(", ", failedBackends)
                                    : string.Empty));
                ShowNotice(_guidanceBox, _guidanceText, NoticeKind.Info,
                    string.Join("\n", PhoneImportErrors.NoDeviceGuidance(_english).Select(line => "• " + line)));
            }
            else
            {
                SetDeviceStatus(T($"{N(_deviceChoices.Count)} aygıt bulundu. Listeden telefonu seçin.",
                    $"{N(_deviceChoices.Count)} device(s) found. Select the phone in the list."));
                if (_deviceChoices.Count == 1 || _pickedDevice is not null)
                    _deviceList.SelectedIndex = 0;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(T("aygıt arama", "device search"), ex);
            SetDeviceStatus(T("Telefonlar aranamadı: ", "Phones couldn't be listed: ") + DescribeException(ex));
        }
        finally
        {
            _suppressDeviceSelection = false;
            if (!token.IsCancellationRequested)
                SetDeviceLoading(false);
            UpdateActionButtons();
        }
    }

    private async Task OpenDeviceAsync(DeviceChoice choice)
    {
        if (_busy || _disposed)
            return;
        var token = RestartDeviceOperation();
        SetDeviceLoading(true, T("Telefon açılıyor…", "Opening the phone…"));
        HideNotice(_guidanceBox);
        ClearFolderView();
        InvalidatePlan();
        try
        {
            if (choice.IsPicked)
            {
                _activeDevice = choice.Device;
                _path = [choice.Device.Root!];
                SetDeviceStatus(T("Windows seçicisinde seçtiğiniz klasör kaynak olarak açıldı.",
                    "The folder you chose in the Windows picker was opened as the source."));
            }
            else
            {
                if (_catalog is null || _discovery is null)
                    return;
                var opened = await _catalog.OpenAsync(choice.Device, _discovery, token);
                if (token.IsCancellationRequested)
                    return;
                _diagnostics.LastOpen = opened;
                if (opened.Error is { } error)
                    _diagnostics.RecordError($"{opened.Device?.BackendName} {T("aygıt açma", "device open")}", error.HResult, error.Message);
                _activeDevice = opened.Device;
                // A device whose listing failed has nothing to browse; the status explains the error.
                _path = opened.Outcome == PortableDeviceOpenOutcome.Failed ? [] : opened.Path.ToList();
                var access = BackendLabel(opened.Device?.BackendName);
                switch (opened.Outcome)
                {
                    case PortableDeviceOpenOutcome.DcimFound:
                        SetDeviceStatus(T($"DCIM klasörü bulundu. Erişim yolu: {access}.",
                            $"DCIM folder found. Access path: {access}."));
                        break;
                    case PortableDeviceOpenOutcome.StorageWithoutDcim:
                        SetDeviceStatus(T("Telefonda DCIM klasörü görünmüyor; klasörü aşağıdan elle seçebilirsiniz.",
                            "No DCIM folder is visible on the phone; you can choose a folder below."));
                        ShowNotice(_guidanceBox, _guidanceText, NoticeKind.Warning, PhoneImportErrors.LockedOrUntrustedHint(_english));
                        break;
                    case PortableDeviceOpenOutcome.StorageEmpty:
                        SetDeviceStatus(T("Telefonun depolaması boş görünüyor.", "The phone's storage appears empty."));
                        ShowNotice(_guidanceBox, _guidanceText, NoticeKind.Warning, PhoneImportErrors.LockedOrUntrustedHint(_english));
                        break;
                    default:
                        var failure = opened.Error;
                        SetDeviceStatus(T("Telefon açılamadı: ", "The phone couldn't be opened: ")
                                        + (failure is null
                                            ? string.Empty
                                            : $"{PhoneImportErrors.Describe(failure.Kind, _english)} ({PhoneImportErrors.FormatHResult(failure.HResult)})"));
                        ShowNotice(_guidanceBox, _guidanceText, NoticeKind.Warning, PhoneImportErrors.LockedOrUntrustedHint(_english));
                        break;
                }
            }
            if (_activeDevice is not null && _path.Count > 0)
                await LoadFolderAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(T("aygıt açma", "device open"), ex);
            SetDeviceStatus(T("Telefon açılamadı: ", "The phone couldn't be opened: ") + DescribeException(ex));
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetDeviceLoading(false);
            UpdateActionButtons();
        }
    }

    private async Task LoadFolderAsync(CancellationToken token)
    {
        if (_activeDevice is null || _path.Count == 0)
            return;
        _breadcrumb.Text = PortableDeviceCatalog.DescribePath(_path, _activeDevice.Name);
        _upButton.IsEnabled = !_busy && _path.Count > 1;
        _folderChoices.Clear();
        _folderStatus.Text = T("Klasör okunuyor…", "Reading the folder…");
        try
        {
            var children = await _activeDevice.Source.GetChildrenAsync(_path[^1], token);
            if (token.IsCancellationRequested)
                return;
            var folders = children.Where(child => child.IsFolder)
                .OrderBy(child => child.Name, StringComparer.Create(_culture, ignoreCase: true))
                .ToArray();
            foreach (var folder in folders)
                _folderChoices.Add(new FolderChoice(folder, folder.Name));
            var files = children.Count - folders.Length;
            _folderStatus.Text = T(
                $"Bu klasörde {N(files)} dosya ve {N(folders.Length)} alt klasör var. Klasörün bütün içeriği alt klasörleriyle kopyalanır; bir alt klasörü açmak için üzerine tıklayın.",
                $"This folder has {N(files)} file(s) and {N(folders.Length)} subfolder(s). Everything in it is copied with its subfolders; click a subfolder to open it.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError($"{_activeDevice.BackendName} {T("klasör listesi", "folder listing")}", ex);
            _folderStatus.Text = T("Klasör okunamadı: ", "The folder couldn't be read: ") + DescribeException(ex);
        }
        finally
        {
            UpdateActionButtons();
        }
    }

    private async Task PrepareSummaryAsync()
    {
        await ScanAsync();
        UpdateActionButtons();
    }

    // Returns the new plan, or null when the scan failed or was cancelled (the summary explains why).
    private async Task<PhoneImportPlan?> ScanAsync()
    {
        if (_busy || _activeDevice is null || _path.Count == 0)
            return null;
        if (!TryGetDestination(out var destination))
            return null;
        var device = _activeDevice;
        var folder = _path[^1];
        var displayPath = PortableDeviceCatalog.DescribePath(_path, device.Name);
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        SetBusy(true);
        HideNotice(_summaryWarning);
        _summaryText.Text = T("Telefondaki klasör taranıyor…", "Scanning the phone folder…");
        var progress = new Progress<PhoneImportScanProgress>(value => _summaryText.Text = T(
            $"Taranıyor: {N(value.FilesFound)} dosya, {N(value.FoldersScanned)} klasör…",
            $"Scanning: {N(value.FilesFound)} files, {N(value.FoldersScanned)} folders…"));
        try
        {
            var plan = await Task.Run(() => _engine.PlanAsync(device.Source, folder, displayPath, destination, progress, token), token);
            _plan = plan;
            _diagnostics.LastPlan = plan;
            ShowPlanSummary(plan);
            return plan;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _summaryText.Text = T("Tarama iptal edildi.", "Scan cancelled.");
        }
        catch (PhoneImportException ex)
        {
            _diagnostics.RecordError($"{device.BackendName} {T("tarama", "scan")}", ex);
            _summaryText.Text = T("Telefondaki klasör taranamadı: ", "The phone folder couldn't be scanned: ")
                                + $"{PhoneImportErrors.Describe(ex.Kind, _english)} ({PhoneImportErrors.FormatHResult(ex.HResult)})";
            if (ex.Kind == PhoneImportErrorKind.DeviceDisconnected)
                ShowNotice(_summaryWarning, _summaryWarningText, NoticeKind.Warning, PhoneImportErrors.LockedOrUntrustedHint(_english));
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError($"{device.BackendName} {T("tarama", "scan")}", ex);
            _summaryText.Text = T("Telefondaki klasör taranamadı: ", "The phone folder couldn't be scanned: ") + DescribeException(ex);
        }
        finally
        {
            EndOperation();
        }
        return null;
    }

    private async Task StartImportAsync()
    {
        if (_busy || _plan is not { FileCount: > 0 } plan)
            return;
        await ImportAsync(plan);
    }

    private async Task RetryAsync()
    {
        if (_busy)
            return;
        var plan = await ScanAsync();
        if (plan is { FileCount: > 0 })
            await ImportAsync(plan);
        UpdateActionButtons();
    }

    private async Task ImportAsync(PhoneImportPlan plan)
    {
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        SetBusy(true);
        HideNotice(_resultBox);
        _failureText.Visibility = Visibility.Collapsed;
        _progressBar.Value = 0;
        _progressBar.Visibility = Visibility.Visible;
        _progressText.Text = T("Aktarma başlıyor…", "Starting the import…");
        var progress = new Progress<PhoneImportProgress>(value => UpdateProgress(plan, value));
        try
        {
            var result = await Task.Run(() => _engine.ImportAsync(plan, progress, token), CancellationToken.None);
            _lastResult = result;
            _diagnostics.LastResult = result;
            foreach (var failure in result.Failures.Take(5))
                _diagnostics.RecordError($"{result.BackendName} {failure.RelativePath}", failure.HResult, failure.Message);
            ShowResult(result);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError($"{plan.Source.BackendName} {T("aktarma", "import")}", ex);
            ShowNotice(_resultBox, _resultText, NoticeKind.Error,
                T("Aktarma başlatılamadı: ", "The import couldn't start: ") + DescribeException(ex));
            Announce(_resultText);
        }
        finally
        {
            EndOperation();
            // The destination changed; a new start needs a fresh summary (or "Retry", which scans first).
            InvalidatePlan();
            _progressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateProgress(PhoneImportPlan plan, PhoneImportProgress value)
    {
        var byBytes = value.TotalBytes > 0 && plan.UnknownSizeCount == 0;
        _progressBar.Value = byBytes
            ? Math.Clamp(value.ProcessedBytes / (double)value.TotalBytes, 0, 1)
            : Math.Clamp(value.ProcessedFiles / (double)Math.Max(1, value.TotalFiles), 0, 1);
        var line = T(
            $"{N(value.ProcessedFiles)}/{N(value.TotalFiles)} dosya • {FormatBytes(value.ProcessedBytes)} / {FormatBytes(value.TotalBytes)}",
            $"{N(value.ProcessedFiles)}/{N(value.TotalFiles)} files • {FormatBytes(value.ProcessedBytes)} / {FormatBytes(value.TotalBytes)}");
        if (value.CurrentItem is { } current)
            line += "\n" + T("Şu an: ", "Now: ") + current;
        line += "\n" + T(
            $"Aktarılan {N(value.CopiedFiles)} • zaten var {N(value.SkippedFiles)} • başarısız {N(value.FailedFiles)}",
            $"Copied {N(value.CopiedFiles)} • already present {N(value.SkippedFiles)} • failed {N(value.FailedFiles)}");
        _progressText.Text = line;
    }

    private void ShowPlanSummary(PhoneImportPlan plan)
    {
        if (plan.FileCount == 0)
        {
            _summaryText.Text = T("Seçili klasörde kopyalanacak dosya yok.", "There are no files to copy in the selected folder.");
            Announce(_summaryText);
            return;
        }
        var free = plan.FreeBytes is { } bytes ? FormatBytes(bytes) : T("bilinmiyor", "unknown");
        var lines = new List<string>
        {
            T($"{N(plan.FileCount)} dosya • telefonun bildirdiği toplam {FormatBytes(plan.ReportedTotalBytes)}",
                $"{N(plan.FileCount)} files • {FormatBytes(plan.ReportedTotalBytes)} reported by the phone"),
            plan.ExistingCount > 0
                ? T($"{N(plan.ExistingCount)} dosya hedefte zaten var ve atlanacak • kopyalanacak {N(plan.FilesToCopy)} dosya, yaklaşık {FormatBytes(plan.ReportedBytesToCopy)}",
                    $"{N(plan.ExistingCount)} files already exist at the destination and will be skipped • {N(plan.FilesToCopy)} files to copy, about {FormatBytes(plan.ReportedBytesToCopy)}")
                : T($"Kopyalanacak {N(plan.FilesToCopy)} dosya, yaklaşık {FormatBytes(plan.ReportedBytesToCopy)}",
                    $"{N(plan.FilesToCopy)} files to copy, about {FormatBytes(plan.ReportedBytesToCopy)}"),
            T($"Hedefteki boş alan: {free}", $"Free space at the destination: {free}"),
            T($"Kaynak: {plan.SourceDisplayPath}", $"Source: {plan.SourceDisplayPath}"),
            T($"Hedef: {plan.DestinationRoot}", $"Destination: {plan.DestinationRoot}")
        };
        if (plan.FilesToCopy == 0)
            lines.Add(T("Tüm dosyalar hedefte zaten var; başlatırsanız hepsi atlanır.",
                "All files already exist at the destination; starting would skip them all."));
        if (plan.UnknownSizeCount > 0)
            lines.Add(T($"{N(plan.UnknownSizeCount)} dosyanın boyutu telefon tarafından bildirilmedi.",
                $"The phone didn't report the size of {N(plan.UnknownSizeCount)} file(s)."));
        if (plan.RenamedCount > 0)
            lines.Add(T($"{N(plan.RenamedCount)} dosyanın adı ya da klasörü Windows'ta geçersiz veya çakışıyor; güvenli bir ada çevrilecek.",
                $"{N(plan.RenamedCount)} file name(s) or their folders are invalid or collide in Windows; they will get a safe name."));
        if (plan.ScanFailures.Count > 0)
            lines.Add(T($"{N(plan.ScanFailures.Count)} alt klasör okunamadı, içindekiler kopyalanmayacak: ",
                          $"{N(plan.ScanFailures.Count)} subfolder(s) couldn't be read and won't be copied: ")
                      + string.Join(", ", plan.ScanFailures.Take(5).Select(failure =>
                          $"{failure.RelativePath} ({PhoneImportErrors.FormatHResult(failure.HResult)})")));
        _summaryText.Text = string.Join("\n", lines);
        if (!plan.HasEnoughSpace)
        {
            ShowNotice(_summaryWarning, _summaryWarningText, NoticeKind.Warning, T(
                $"Hedefte yeterli boş alan görünmüyor: yaklaşık {FormatBytes(plan.ReportedBytesToCopy)} gerekli, {free} boş. Telefonun bildirdiği boyut gerçek boyuttan farklı olabilir. Yine de başlatabilirsiniz; yer biterse aktarma durur ve yarım dosya bırakmaz.",
                $"The destination doesn't appear to have enough free space: about {FormatBytes(plan.ReportedBytesToCopy)} is needed and {free} is free. The size reported by the phone can differ from the real size. You can still start; if space runs out, the import stops without leaving a partial file."));
        }
        Announce(_summaryText);
    }

    private void ShowResult(PhoneImportResult result)
    {
        var counts = T(
            $"{N(result.CopiedCount)} dosya aktarıldı ({FormatBytes(result.CopiedBytes)}), {N(result.SkippedExistingCount)} dosya zaten vardı, {N(result.FailedCount)} başarısız, {N(result.NotProcessedCount)} işlenmedi.",
            $"{N(result.CopiedCount)} file(s) copied ({FormatBytes(result.CopiedBytes)}), {N(result.SkippedExistingCount)} already present, {N(result.FailedCount)} failed, {N(result.NotProcessedCount)} not processed.");
        var (kind, headline) = result.Status switch
        {
            PhoneImportStatus.Completed when result.CopiedCount == 0 && result.SkippedExistingCount == result.TotalCount =>
                (NoticeKind.Success, T($"Yeni dosya yok: {N(result.SkippedExistingCount)} dosyanın hepsi hedefte zaten var.",
                    $"Nothing new: all {N(result.SkippedExistingCount)} files already exist at the destination.")),
            PhoneImportStatus.Completed =>
                (NoticeKind.Success, T("Aktarma tamamlandı. ", "Import completed. ") + counts),
            PhoneImportStatus.CompletedWithErrors =>
                (NoticeKind.Warning, T("Aktarma hatalarla tamamlandı. ", "Import completed with errors. ") + counts
                    + " " + T("“Yeniden dene” yalnız eksik dosyaları alır.", "“Retry” fetches only the missing files.")),
            PhoneImportStatus.Cancelled =>
                (NoticeKind.Warning, T("Aktarma iptal edildi. ", "Import cancelled. ") + counts
                    + " " + T("Yarım dosya bırakılmadı; “Yeniden dene” kaldığı yerden devam eder.",
                        "No partial file was left; “Retry” resumes where it stopped.")),
            PhoneImportStatus.StoppedDeviceDisconnected =>
                (NoticeKind.Error, T("Telefonla bağlantı koptu, aktarma durdu. ", "The connection to the phone was lost and the import stopped. ") + counts
                    + " " + T("Telefonu yeniden bağlayıp kilidini açın, “Yenile”ye ve sonra “Yeniden dene”ye basın; aktarılanlar atlanır.",
                        "Reconnect and unlock the phone, select “Refresh” and then “Retry”; files already copied are skipped.")),
            _ =>
                (NoticeKind.Error, T("Hedef sürücü doldu, aktarma durdu. ", "The destination drive is full and the import stopped. ") + counts
                    + " " + T("Yer açıp “Yeniden dene”ye basın; aktarılanlar atlanır.", "Free up space and select “Retry”; files already copied are skipped."))
        };
        var details = new List<string>
        {
            T($"Erişim yolu: {BackendLabel(result.BackendName)} • süre {FormatDuration(result.Elapsed)}",
                $"Access path: {BackendLabel(result.BackendName)} • duration {FormatDuration(result.Elapsed)}")
        };
        if (result.SizeMismatchCount > 0)
            details.Add(T($"{N(result.SizeMismatchCount)} dosyanın boyutu telefonun bildirdiğinden farklı çıktı (iPhone “Otomatik” biçimde dönüştürmüş olabilir); içerik okunan veriyle doğrulandı.",
                $"{N(result.SizeMismatchCount)} file(s) differed in size from what the phone reported (the iPhone may have converted them in “Automatic” format); the content was verified against the data read."));
        if (result.RenamedCount > 0)
            details.Add(T($"{N(result.RenamedCount)} dosya Windows'ta geçersiz ya da çakışan adı yüzünden güvenli bir adla kaydedildi.",
                $"{N(result.RenamedCount)} file(s) were saved under a safe name because their name was invalid or collided in Windows."));
        if (result.SuggestKeepOriginals)
            details.Add(PhoneImportErrors.KeepOriginalsHint(_english));
        ShowNotice(_resultBox, _resultText, kind, headline + "\n" + string.Join("\n", details));

        if (result.Failures.Count > 0)
        {
            var lines = result.Failures.Select(failure =>
                $"• {failure.RelativePath} — {PhoneImportErrors.Describe(failure.Kind, _english)} ({PhoneImportErrors.FormatHResult(failure.HResult)})");
            var more = result.FailedCount - result.Failures.Count;
            _failureText.Text = T($"Başarısız dosyalar (ilk {N(result.Failures.Count)}):", $"Failed files (first {N(result.Failures.Count)}):")
                                + "\n" + string.Join("\n", lines)
                                + (more > 0 ? "\n" + T($"…ve {N(more)} dosya daha", $"…and {N(more)} more") : string.Empty);
            _failureText.Visibility = Visibility.Visible;
        }
        _progressText.Text = string.Empty;
        Announce(_resultText);
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;
            if (!string.IsNullOrEmpty(folder.Path))
            {
                SetDeviceStatus(T(
                    "Seçilen klasörün normal bir dosya yolu var; onu Transferler bölümünde doğrudan kaynak olarak seçebilirsiniz.",
                    "The selected folder has a normal file path; you can choose it directly as the source in Transfers."));
                return;
            }
            UsePickedFolder(folder);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            _diagnostics.RecordError(T("klasör seçici", "folder picker"), ex);
            SetDeviceStatus(T("Windows klasör seçicisi açılamadı: ", "The Windows folder picker couldn't open: ") + ex.Message);
        }
    }

    private async void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;
            // A phone folder comes back without a file-system path; it can't be the destination.
            var kind = string.IsNullOrEmpty(folder.Path)
                ? TransferPathKind.PortableDevice
                : TransferPathClassifier.Classify(folder.Path);
            if (kind != TransferPathKind.FileSystem)
            {
                _summaryText.Text = DestinationKindMessage(kind);
                return;
            }
            _destinationBox.Text = folder.Path;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            _summaryText.Text = T("Windows klasör seçicisi açılamadı. Tam yolu kutuya yazabilir veya yapıştırabilirsiniz.",
                "The Windows folder picker couldn't open. Type or paste the full path in the box.");
        }
    }

    private bool TryGetDestination(out string destination)
    {
        destination = string.Empty;
        var kind = TransferPathClassifier.Classify(_destinationBox.Text);
        if (kind != TransferPathKind.FileSystem)
        {
            _summaryText.Text = DestinationKindMessage(kind);
            Announce(_summaryText);
            return false;
        }
        try
        {
            destination = PhoneImportEngine.NormalizeDestination(_destinationBox.Text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _summaryText.Text = T("Hedef yol geçersiz: ", "The destination path is invalid: ") + ex.Message;
            return false;
        }
        if (File.Exists(destination))
        {
            _summaryText.Text = T("Hedef bir dosya; bir klasör seçin.", "The destination is a file; choose a folder.");
            return false;
        }
        return true;
    }

    private string DestinationKindMessage(TransferPathKind kind) => kind switch
    {
        TransferPathKind.Empty => T("Önce bu bilgisayardaki bir hedef klasör seçin.", "First choose a destination folder on this PC."),
        TransferPathKind.NotRooted => T(@"Hedef için tam bir klasör yolu girin (ör. D:\Fotoğraflar).",
            @"Enter a full folder path for the destination (for example D:\Photos)."),
        _ => T("Hedef, bu bilgisayardaki ya da harici bir sürücüdeki bir klasör olmalı; telefon veya Windows'un sanal konumları hedef seçilemez.",
            "The destination must be a folder on this PC or on an external drive; a phone or a virtual Windows location can't be the destination.")
    };

    private void OpenDestination()
    {
        var destination = _lastResult?.DestinationRoot;
        if (destination is null || !Directory.Exists(destination))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _diagnosticsStatus.Text = T("Hedef klasör açılamadı: ", "The destination folder couldn't be opened: ") + ex.Message;
        }
    }

    private void CopyDiagnostics()
    {
        _copyDiagnosticsButton.IsEnabled = false;
        _diagnosticsStatus.Text = T("Tanılama bilgisi hazırlanıyor…", "Preparing diagnostic information…");
        try
        {
            _diagnostics.AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
            _diagnostics.Distribution = PortableDistributionDetector.IsPortable(AppContext.BaseDirectory)
                ? "portable"
                : T("kurulu", "installed");
            _diagnostics.BackendPreference = (_backendBox.SelectedItem as BackendChoice)?.Preference.ToString() ?? "Automatic";
            var extra = new List<string>
            {
                $"WinRT: {T("yolu olan sürücü (atlandı)", "drives with a path (skipped)")} {_winRt.SkippedDrivesWithPath}"
            };
            if (_pickedDevice is not null)
                extra.Add($"{T("Seçiciyle açılan klasör", "Folder opened with the picker")}: \"{_pickedDevice.Name}\"");
            if (_activeDevice is not null && _path.Count > 0)
                extra.Add($"{T("Seçili klasör", "Selected folder")} [{_activeDevice.BackendName}]: {PortableDeviceCatalog.DescribePath(_path, _activeDevice.Name)}");
            _diagnostics.ExtraLines = extra;
            var package = new DataPackage();
            package.SetText(_diagnostics.Build(_english));
            Clipboard.SetContent(package);
            _diagnosticsStatus.Text = T("Tanılama bilgisi panoya kopyalandı; destek konuşmasına yapıştırabilirsiniz.",
                "Diagnostic information was copied to the clipboard; you can paste it into a support conversation.");
        }
        catch (Exception ex)
        {
            _diagnosticsStatus.Text = T("Tanılama bilgisi kopyalanamadı: ", "Diagnostic information couldn't be copied: ") + ex.Message;
        }
        finally
        {
            _copyDiagnosticsButton.IsEnabled = true;
            Announce(_diagnosticsStatus);
        }
    }

    private IReadOnlyList<IPortableDeviceSource> SelectedSources() =>
        (_backendBox.SelectedItem as BackendChoice)?.Preference switch
        {
            BackendPreference.WinRt => [_winRt],
            BackendPreference.Shell => [_shell],
            _ => [_winRt, _shell]
        };

    private CancellationToken RestartDeviceOperation()
    {
        _deviceOperation?.Cancel();
        _deviceOperation = new CancellationTokenSource();
        return _deviceOperation.Token;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _cancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _cancelButton.IsEnabled = busy;
        UpdateActionButtons();
    }

    private void EndOperation()
    {
        _operation?.Dispose();
        _operation = null;
        SetBusy(false);
    }

    private void UpdateActionButtons()
    {
        var idle = !_busy;
        var hasFolder = _activeDevice is not null && _path.Count > 0;
        _deviceList.IsEnabled = idle;
        _refreshButton.IsEnabled = idle;
        _backendBox.IsEnabled = idle;
        _pickFolderButton.IsEnabled = idle;
        _folderList.IsEnabled = idle;
        _upButton.IsEnabled = idle && _path.Count > 1;
        _destinationBox.IsEnabled = idle;
        _browseButton.IsEnabled = idle;
        _scanButton.IsEnabled = idle && hasFolder && !string.IsNullOrWhiteSpace(_destinationBox.Text);
        _startButton.IsEnabled = idle && _plan is { FileCount: > 0 };
        _startButton.Content = _plan is { HasEnoughSpace: false }
            ? T("Yine de başlat", "Start anyway")
            : T("Aktarmayı başlat", "Start import");
        _retryButton.Visibility = _lastResult is null ? Visibility.Collapsed : Visibility.Visible;
        _retryButton.IsEnabled = idle && hasFolder;
        _openDestinationButton.Visibility = _lastResult is not null && Directory.Exists(_lastResult.DestinationRoot)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _copyDiagnosticsButton.IsEnabled = true;
    }

    private void InvalidatePlan()
    {
        _plan = null;
        if (!_busy)
        {
            _summaryText.Text = T(
                "Hedefi seçip “Özeti hazırla”ya basın: dosya sayısı, toplam boyut ve hedefteki boş alan gösterilir.",
                "Choose the destination and select “Prepare summary” to see the file count, total size, and free space at the destination.");
            HideNotice(_summaryWarning);
        }
        UpdateActionButtons();
    }

    private void ClearFolderView()
    {
        _activeDevice = null;
        _path = [];
        _folderChoices.Clear();
        _breadcrumb.Text = string.Empty;
        _upButton.IsEnabled = false;
        _folderStatus.Text = T("Önce bir telefon seçin.", "Select a phone first.");
    }

    private void ShowIdleState()
    {
        ClearFolderView();
        InvalidatePlan();
        _progressText.Text = T("Henüz aktarma yapılmadı.", "No import has run yet.");
        HideNotice(_guidanceBox);
        HideNotice(_resultBox);
    }

    private void SetDeviceLoading(bool loading, string? message = null)
    {
        _deviceRing.IsActive = loading;
        _deviceRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null)
            SetDeviceStatus(message);
    }

    private void SetDeviceStatus(string message)
    {
        _deviceStatus.Text = message;
        Announce(_deviceStatus);
    }

    private string DeviceLabel(PortableDeviceInfo device) =>
        $"{device.Name}  ·  {BackendLabel(device.BackendName)}"
        + (device.Root is null ? "  ·  " + T("açılamadı", "couldn't be opened") : string.Empty);

    private string PickedLabel(PortableDeviceInfo device) =>
        $"{device.Name}  ·  {T("seçiciyle açılan klasör", "folder opened with the picker")}";

    private string BackendLabel(string? backend) => backend switch
    {
        WinRtPortableDeviceSource.Name => T("Windows depolama (WinRT)", "Windows storage (WinRT)"),
        ShellPortableDeviceSource.Name => T("Dosya Gezgini kabuğu", "File Explorer shell"),
        _ => backend ?? "—"
    };

    private string DescribeException(Exception exception)
    {
        var kind = PhoneImportErrors.Classify(exception);
        return $"{PhoneImportErrors.Describe(kind, _english)} ({PhoneImportErrors.FormatHResult(exception.HResult)}) {exception.Message}".Trim();
    }

    private string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return size.ToString("0.##", _culture) + " " + units[unit];
    }

    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{value.Minutes:00}:{value.Seconds:00}";

    private string N(int value) => value.ToString("N0", _culture);

    private string T(string turkish, string english) => _english ? english : turkish;

    private static void Announce(FrameworkElement element)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(element)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private enum NoticeKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    private static TextBlock NoticeText() => new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

    private static Border NoticeBox(TextBlock text)
    {
        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9, 12, 9),
            Visibility = Visibility.Collapsed,
            Child = text
        };
    }

    private static void ShowNotice(Border box, TextBlock text, NoticeKind kind, string message)
    {
        // Same tones as the transfer page's validation, preflight, and update bars.
        var (background, border, foreground) = kind switch
        {
            NoticeKind.Success => (ColorBrush(0x15, 0x2A, 0x26), ColorBrush(0x2E, 0x87, 0x6F), ColorBrush(0xA5, 0xE5, 0xD0)),
            NoticeKind.Warning => (ColorBrush(0x2B, 0x22, 0x16), ColorBrush(0xB7, 0x85, 0x3F), ColorBrush(0xFF, 0xD9, 0xA0)),
            NoticeKind.Error => (ColorBrush(0x32, 0x1C, 0x24), ColorBrush(0xC9, 0x53, 0x66), ColorBrush(0xFF, 0xB7, 0xC1)),
            _ => (ColorBrush(0x20, 0x1C, 0x3B), ColorBrush(0x6D, 0x5D, 0xFB), ColorBrush(0xDD, 0xD9, 0xFF))
        };
        box.Background = background;
        box.BorderBrush = border;
        text.Foreground = foreground;
        text.Text = message;
        box.Visibility = Visibility.Visible;
    }

    private static void HideNotice(Border box) => box.Visibility = Visibility.Collapsed;

    private static TextBlock Muted() => new()
    {
        Style = GetStyle("MutedTextStyle"),
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        Style = GetStyle("SectionTitleStyle"),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Border Card(UIElement child) => new()
    {
        Style = GetStyle("CardStyle"),
        Child = child
    };

    private static Style? GetStyle(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : ColorBrush(0x9D, 0xA5, 0xB5);

    private static SolidColorBrush ColorBrush(byte red, byte green, byte blue) =>
        new(Microsoft.UI.ColorHelper.FromArgb(255, red, green, blue));

    private enum BackendPreference
    {
        Automatic,
        WinRt,
        Shell
    }

    private sealed record DeviceChoice(PortableDeviceInfo Device, string Label, bool IsPicked);

    private sealed record FolderChoice(PortableDeviceEntry Entry, string Label);

    private sealed record BackendChoice(BackendPreference Preference, string Label);
}
