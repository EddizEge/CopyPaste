using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CopyPaste.App.Services;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CopyPaste.App;

public sealed partial class MainWindow : Window
{
    private readonly RobocopyRunner _runner = new();
    private readonly CopyPreflightAnalyzer _preflightAnalyzer = new();
    private readonly CopyVerificationService _verificationService = new();
    private readonly CopyComparisonService _comparisonService = new();
    private readonly HistoryStore _historyStore = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly JobLogStore _jobLogStore = new();
    private readonly QueueStateStore _queueStateStore = new();
    private readonly GitHubUpdateService _updateService = new();
    private readonly UpdateDownloadService _updateDownloadService = new();
    private readonly FailedItemRetryService _failedItemRetryService;
    private readonly TransferReportService _reportService = new();
    private readonly NetworkAvailabilityService _networkAvailability = new();
    private readonly ScheduleStore _scheduleStore = new();
    private readonly TaskSchedulerService _taskScheduler = new();
    private readonly DiagnosticsService _diagnosticsService = new();
    private readonly AppNotificationService _notificationService;
    private readonly ExplorerIntegrationService _explorerIntegration = new();
    private readonly TrayIconService _trayIcon;
    private readonly AppWindow _appWindow;
    private readonly ObservableCollection<QueueItemViewModel> _queue = [];
    private readonly ObservableCollection<CopyProfile> _profiles = new(CopyProfiles.All);
    private CancellationTokenSource? _copyCancellation;
    private QueueItemViewModel? _activeQueueItem;
    private bool _isQueueRunning;
    private bool _pauseRequested;
    private AppSettings _settings = new();
    private string _language = "tr-TR";
    private Uri? _availableUpdateUri;
    private UpdateCheckResult? _availableUpdate;
    private string? _downloadedUpdatePath;
    private bool _isUpdateCheckRunning;
    private bool _closeConfirmed;
    private bool _closeDialogOpen;
    private bool _settingsDialogOpen;
    private bool _useBackupModeForSource;
    private bool _rootLoaded;
    private CopyJob? _lastResultJob;
    private readonly CopyJob? _startupJob;
    private readonly IReadOnlyList<string> _startupSourcePaths;
    private readonly string? _startupDestinationPath;

    public MainWindow(ShellLaunchRequest? shellRequest = null, AppNotificationService? notificationService = null)
    {
        InitializeComponent();
        _startupJob = shellRequest?.ScheduledJob;
        _startupSourcePaths = shellRequest?.SourcePaths ?? [];
        _startupDestinationPath = shellRequest?.DestinationPath;
        _failedItemRetryService = new FailedItemRetryService(_runner);
        _notificationService = notificationService ?? new AppNotificationService();
        _trayIcon = new TrayIconService(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        _appWindow = AppWindow.GetFromWindowId(windowId);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        _appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        _appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1380, 880));
        _appWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        ProfileComboBox.ItemsSource = _profiles;
        ProfileComboBox.SelectedIndex = 0;
        ApplyLanguageChoices();
        QueueListView.ItemsSource = _queue;
        if (!string.IsNullOrWhiteSpace(shellRequest?.SourcePath))
            SourceTextBox.Text = shellRequest.SourcePath;
        if (!string.IsNullOrWhiteSpace(shellRequest?.DestinationPath))
            DestinationTextBox.Text = shellRequest.DestinationPath;
        if (!string.IsNullOrWhiteSpace(shellRequest?.Message))
        {
            if (shellRequest.SourcePath is not null)
            {
                PreflightText.Text = shellRequest.Message;
                PreflightInfoBar.Visibility = Visibility.Visible;
            }
            else
            {
                ValidationText.Text = shellRequest.Message;
                ValidationInfoBar.Visibility = Visibility.Visible;
            }
        }
        UpdateIntegrationState();
        UpdateQueueState();
        _ = InitializeAsync(shellRequest?.AutoStart == true);
    }

    private async Task InitializeAsync(bool autoStart)
    {
        await LoadSettingsAsync();
        await LoadQueueStateAsync();
        await LoadHistoryAsync();
        if (_diagnosticsService.HasCrashReport)
        {
            ValidationText.Text = T("Önceki oturum beklenmedik şekilde kapandı. Ayarlar içinden tanılama paketi oluşturabilirsiniz.",
                "The previous session closed unexpectedly. You can create a diagnostics package from Settings.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            _diagnosticsService.AcknowledgeCrashReport();
        }
        if (_startupJob is not null)
            LoadJobIntoForm(_startupJob);
        if (autoStart && _startupSourcePaths.Count > 1 && !string.IsNullOrWhiteSpace(_startupDestinationPath))
        {
            foreach (var sourcePath in _startupSourcePaths)
            {
                SourceTextBox.Text = sourcePath;
                DestinationTextBox.Text = _startupDestinationPath;
                await AddCurrentTransferToQueueAsync();
            }
            await RunQueueAsync();
        }
        else if (autoStart && await AddCurrentTransferToQueueAsync())
        {
            await RunQueueAsync();
        }
        if (_settings.CheckForUpdatesOnStartup)
            await CheckForUpdatesAsync(manual: false);
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_rootLoaded)
            return;
        _rootLoaded = true;
        ApplyLanguage(_settings.Language);
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(manual: true);

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_isUpdateCheckRunning)
            return;

        _isUpdateCheckRunning = true;
        if (manual)
        {
            CheckUpdatesButton.IsEnabled = false;
            CheckUpdatesButton.Content = T("Kontrol ediliyor…", "Checking…");
        }
        try
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 5, 0, 0);
            var result = await _updateService.CheckAsync(currentVersion);
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    _availableUpdate = result;
                    _availableUpdateUri = result.DownloadUri ?? result.ReleasePageUri;
                    UpdateStatusText.Text = T($"Yeni sürüm hazır: {result.TagName}. Yüklü sürüm: {result.CurrentVersion.ToString(3)}.",
                        $"New version available: {result.TagName}. Installed: {result.CurrentVersion.ToString(3)}.");
                    OpenUpdateButton.Content = T("İndir", "Download");
                    OpenUpdateButton.Visibility = _availableUpdateUri is null
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    UpdateInfoBar.Visibility = Visibility.Visible;
                    if (_settings.NotificationsEnabled)
                        _notificationService.ShowUpdateAvailable(result.TagName ?? result.LatestVersion?.ToString(3) ?? "Yeni");
                    if (_settings.AutoDownloadUpdates && !string.IsNullOrWhiteSpace(result.Sha256Digest))
                        _ = DownloadUpdateAsync(result, automatic: true);
                    break;
                case UpdateCheckStatus.UpToDate when manual:
                    _availableUpdate = null;
                    _availableUpdateUri = ProductInfo.LatestReleaseUri;
                    UpdateStatusText.Text = T($"CopyPaste güncel — {result.CurrentVersion.ToString(3)}.",
                        $"CopyPaste is up to date — {result.CurrentVersion.ToString(3)}.");
                    OpenUpdateButton.Content = T("Sürümleri aç", "Open releases");
                    OpenUpdateButton.Visibility = Visibility.Visible;
                    UpdateInfoBar.Visibility = Visibility.Visible;
                    break;
                case UpdateCheckStatus.RepositoryUnavailable when manual:
                    ShowUpdateCheckMessage(T("GitHub deposu veya yayınlanmış bir sürüm henüz bulunamadı.",
                        "The GitHub repository or a published release is not available yet."));
                    break;
                case UpdateCheckStatus.NetworkError or UpdateCheckStatus.InvalidResponse when manual:
                    ShowUpdateCheckMessage(T("Güncellemeler kontrol edilemedi. İnternet bağlantısını deneyip tekrar kontrol edin.",
                        "Could not check for updates. Check your internet connection and try again."));
                    break;
            }
        }
        finally
        {
            _isUpdateCheckRunning = false;
            if (manual)
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = T("Güncellemeleri kontrol et", "Check for updates");
            }
        }
    }

    private void ShowUpdateCheckMessage(string message)
    {
        _availableUpdate = null;
        _availableUpdateUri = null;
        UpdateStatusText.Text = message;
        OpenUpdateButton.Visibility = Visibility.Collapsed;
        UpdateInfoBar.Visibility = Visibility.Visible;
    }

    private async void OpenUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is not { HasUpdate: true } update)
        {
            if (_availableUpdateUri is not null)
                Process.Start(new ProcessStartInfo(_availableUpdateUri.AbsoluteUri) { UseShellExecute = true });
            return;
        }
        if (_isQueueRunning)
        {
            UpdateStatusText.Text = T("Aktif transfer tamamlandıktan sonra güncellemeyi kurabilirsiniz.",
                "You can install the update after the active transfer finishes.");
            return;
        }

        var updatePath = File.Exists(_downloadedUpdatePath) ? _downloadedUpdatePath : await DownloadUpdateAsync(update);
        if (string.IsNullOrWhiteSpace(updatePath))
            return;

        UpdateStatusText.Text = T("Güncelleme SHA-256 ile doğrulandı. Kurulum başlatılıyor…",
            "Update verified with SHA-256. Starting setup…");
        OpenUpdateButton.Content = T("Doğrulandı", "Verified");
        Process.Start(new ProcessStartInfo(updatePath)
        {
            UseShellExecute = true,
            Arguments = "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
        });
    }

    private async Task<string?> DownloadUpdateAsync(UpdateCheckResult update, bool automatic = false)
    {
        OpenUpdateButton.IsEnabled = false;
        OpenUpdateButton.Content = automatic ? T("Hazırlanıyor…", "Preparing…") : T("İndiriliyor…", "Downloading…");
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            OpenUpdateButton.Content = value.Percentage is { } percentage
                ? T($"İndiriliyor %{percentage:0}", $"Downloading {percentage:0}%")
                : T($"İndiriliyor {QueueItemViewModel.FormatBytes(value.BytesReceived)}",
                    $"Downloading {QueueItemViewModel.FormatBytes(value.BytesReceived)}");
        });
        var updatesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste", "Updates");
        var download = await _updateDownloadService.DownloadAsync(update, updatesDirectory, progress);
        if (!download.Success || string.IsNullOrWhiteSpace(download.FilePath))
        {
            UpdateStatusText.Text = download.Error ?? T("Güncelleme indirilemedi.", "The update could not be downloaded.");
            OpenUpdateButton.Content = T("Tekrar dene", "Try again");
            OpenUpdateButton.IsEnabled = true;
            return null;
        }

        _downloadedUpdatePath = download.FilePath;
        UpdateStatusText.Text = T($"{update.TagName} indirildi ve SHA-256 ile doğrulandı.",
            $"{update.TagName} downloaded and verified with SHA-256.");
        OpenUpdateButton.Content = T("Kur ve yeniden başlat", "Install and restart");
        OpenUpdateButton.IsEnabled = true;
        return download.FilePath;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        if ((sender as FrameworkElement)?.Tag?.ToString() == "Source")
        {
            SourceTextBox.Text = folder.Path;
            _useBackupModeForSource = false;
        }
        else
            DestinationTextBox.Text = folder.Path;
    }

    private async void ProtectedSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await ProtectedFolderPickerService.PickAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;
        SourceTextBox.Text = path;
        _useBackupModeForSource = true;
        PreflightText.Text = T("Korumalı kaynak seçildi. Sahiplik ve klasör izinleri değiştirilmedi.",
            "Protected source selected. Ownership and folder permissions were not changed.");
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateProfileDescription();

    private void UpdateProfileDescription()
    {
        if (ProfileComboBox.SelectedItem is CopyProfile profile)
            ProfileDescriptionText.Text = $"{profile.Description} • {profile.ThreadCount} " +
                                          T("paralel iş parçacığı", "parallel threads");
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await AddCurrentTransferToQueueAsync();
    }

    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationInfoBar.Visibility = Visibility.Collapsed;
        var draft = CreateDraftJob();
        if (draft is null)
            return;

        CompareButton.IsEnabled = false;
        CompareButton.Content = T("Karşılaştırılıyor…", "Comparing…");
        try
        {
            var progress = new Progress<CopyVerificationProgress>(value =>
                PreflightText.Text = T($"Karşılaştırılıyor: {value.CheckedFiles:N0} dosya",
                    $"Comparing: {value.CheckedFiles:N0} files"));
            PreflightInfoBar.Visibility = Visibility.Visible;
            var comparison = await _comparisonService.CompareAsync(draft, progress);
            var lines = comparison.Differences.Take(500)
                .Select(item => $"{item.RelativePath}\n{item.Detail}")
                .ToList();
            if (lines.Count == 0)
                lines.Add(T("Kaynak ve hedef eşleşiyor; onarım gerekmiyor.",
                    "Source and destination match; no repair is needed."));
            var list = new ListView
            {
                ItemsSource = lines,
                MinWidth = 680,
                MaxHeight = 390,
                SelectionMode = ListViewSelectionMode.None
            };
            var summary = new TextBlock
            {
                Text = T(
                    $"{comparison.CheckedFiles:N0} kontrol • {comparison.IdenticalFiles:N0} aynı • " +
                    $"{comparison.MissingFiles:N0} eksik • {comparison.SizeMismatches:N0} boyut farkı • " +
                    $"{comparison.HashMismatches:N0} hash farkı • {comparison.ReadErrors:N0} okuma hatası",
                    $"{comparison.CheckedFiles:N0} checked • {comparison.IdenticalFiles:N0} identical • " +
                    $"{comparison.MissingFiles:N0} missing • {comparison.SizeMismatches:N0} size mismatches • " +
                    $"{comparison.HashMismatches:N0} hash mismatches • {comparison.ReadErrors:N0} read errors"),
                TextWrapping = TextWrapping.Wrap
            };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(summary);
            panel.Children.Add(list);
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = T("Kuru çalışma ve karşılaştırma", "Dry run and comparison"),
                Content = panel,
                PrimaryButtonText = T("Eksik ve bozukları onar", "Repair missing and damaged files"),
                CloseButtonText = T("Kapat", "Close"),
                IsPrimaryButtonEnabled = comparison.NeedsRepair
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
            var repairJobs = CopyRepairService.CreateRepairJobs(draft, comparison.Differences);
            foreach (var repairJob in repairJobs)
            {
                var preflight = await _preflightAnalyzer.AnalyzeAsync(
                    repairJob.SourcePath, repairJob.DestinationPath, repairJob.Options);
                repairJob.EstimatedFileCount = preflight.FileCount;
                repairJob.EstimatedTotalBytes = preflight.TotalBytes;
                _queue.Add(new QueueItemViewModel(repairJob, preflight, LocalizationService.IsEnglish(_language)));
            }
            await SaveQueueStateAsync();
            UpdateQueueState();
            PreflightText.Text = T($"{repairJobs.Count:N0} onarım işi kuyruğa eklendi.",
                $"{repairJobs.Count:N0} repair jobs were added to the queue.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = T("Karşılaştırma tamamlanamadı: ", "Comparison could not be completed: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
        finally
        {
            CompareButton.IsEnabled = !_isQueueRunning;
            CompareButton.Content = T("Önizle / karşılaştır", "Preview / compare");
        }
    }

    private CopyJob? CreateDraftJob()
    {
        if (ProfileComboBox.SelectedItem is not CopyProfile profile
            || ExistingFileBehaviorComboBox.SelectedItem is not OptionChoice<ExistingFileBehavior> existingChoice
            || VerificationModeComboBox.SelectedItem is not OptionChoice<VerificationMode> verificationChoice)
            return null;
        var options = CopyJobOptionsParser.Parse(existingChoice.Value, verificationChoice.Value,
            FilePatternsTextBox.Text, ExcludedDirectoriesTextBox.Text);
        if (!options.IsValid)
        {
            ValidationText.Text = options.Error;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return null;
        }
        try
        {
            var source = Path.GetFullPath(SourceTextBox.Text.Trim());
            var destinationRoot = Path.GetFullPath(DestinationTextBox.Text.Trim());
            var rootMode = (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
                ?? CopyRootMode.SelectedFolder;
            var destination = CopyDestinationResolver.Resolve(source, destinationRoot, rootMode);
            var validation = CopyJobValidator.Validate(source, destination);
            if (!validation.IsValid)
            {
                ValidationText.Text = validation.Error;
                ValidationInfoBar.Visibility = Visibility.Visible;
                return null;
            }
            return new CopyJob
            {
                SourcePath = source,
                DestinationPath = destination,
                DestinationRootPath = destinationRoot,
                RootMode = rootMode,
                Profile = profile,
                RequestedPerformanceMode = _settings.PerformanceMode,
                BandwidthLimitMbps = _settings.BandwidthLimitMbps,
                UseBackupMode = _useBackupModeForSource,
                Options = options.Options!
            };
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            ValidationText.Text = ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return null;
        }
    }

    private async Task<bool> AddCurrentTransferToQueueAsync()
    {
        ValidationInfoBar.Visibility = Visibility.Collapsed;
        PreflightInfoBar.Visibility = Visibility.Collapsed;
        var rootMode = (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
            ?? CopyRootMode.SelectedFolder;
        string sourcePath;
        string destinationRootPath;
        string destinationPath;
        try
        {
            sourcePath = Path.GetFullPath(SourceTextBox.Text.Trim());
            destinationRootPath = Path.GetFullPath(DestinationTextBox.Text.Trim());
            destinationPath = CopyDestinationResolver.Resolve(sourcePath, destinationRootPath, rootMode);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            ValidationText.Text = ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return false;
        }
        var validation = CopyJobValidator.Validate(sourcePath, destinationPath);
        if (!validation.IsValid)
        {
            ValidationText.Text = validation.Error;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return false;
        }

        if (ProfileComboBox.SelectedItem is not CopyProfile profile)
            return false;

        if (ExistingFileBehaviorComboBox.SelectedItem is not OptionChoice<ExistingFileBehavior> existingChoice
            || VerificationModeComboBox.SelectedItem is not OptionChoice<VerificationMode> verificationChoice)
            return false;

        var optionsResult = CopyJobOptionsParser.Parse(
            existingChoice.Value,
            verificationChoice.Value,
            FilePatternsTextBox.Text,
            ExcludedDirectoriesTextBox.Text);
        if (!optionsResult.IsValid)
        {
            ValidationText.Text = optionsResult.Error;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return false;
        }

        var job = new CopyJob
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            DestinationRootPath = destinationRootPath,
            RootMode = rootMode,
            Profile = profile,
            RequestedPerformanceMode = _settings.PerformanceMode,
            BandwidthLimitMbps = _settings.BandwidthLimitMbps,
            UseBackupMode = _useBackupModeForSource,
            Options = optionsResult.Options!
        };

        _settings = GetSettingsFromUi();
        _settings = _settings with
        {
            RecentSources = RememberPath(_settings.RecentSources, job.SourcePath),
            RecentDestinations = RememberPath(_settings.RecentDestinations, job.DestinationRootPath ?? job.DestinationPath)
        };
        await _settingsStore.SaveAsync(_settings);

        StartButton.IsEnabled = false;
        StartButton.Content = T("Analiz ediliyor…", "Analyzing…");
        try
        {
            var preflight = await _preflightAnalyzer.AnalyzeAsync(job.SourcePath, job.DestinationPath, job.Options);
            if (!preflight.HasEnoughSpace)
            {
                ValidationText.Text = T("Hedef sürücüde bu transfer için yeterli boş alan yok.",
                    "The destination drive does not have enough free space for this transfer.");
                ValidationInfoBar.Visibility = Visibility.Visible;
                return false;
            }

            job.EstimatedTotalBytes = preflight.TotalBytes;
            job.EstimatedFileCount = preflight.FileCount;
            _queue.Add(new QueueItemViewModel(job, preflight, LocalizationService.IsEnglish(_language)));
            await SaveQueueStateAsync();
            var warningText = preflight.Warnings.Count == 0
                ? T("Ön analiz tamamlandı.", "Preflight completed.")
                : string.Join(" ", preflight.Warnings);
            PreflightText.Text = $"{preflight.FileCount:N0} {T("dosya", "files")} • " +
                                 $"{preflight.DirectoryCount:N0} {T("klasör", "folders")} • " +
                                 $"{QueueItemViewModel.FormatBytes(preflight.TotalBytes)}. {warningText}";
            PreflightInfoBar.Visibility = Visibility.Visible;
            UpdateQueueState();
            return true;
        }
        catch (OperationCanceledException)
        {
            ValidationText.Text = T("Ön analiz iptal edildi.", "Preflight was cancelled.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            return false;
        }
        finally
        {
            StartButton.Content = T("Kuyruğa ekle", "Add to queue");
            StartButton.IsEnabled = !_isQueueRunning;
        }
    }

    private async void StartQueueButton_Click(object sender, RoutedEventArgs e)
    {
        await RunQueueAsync();
    }

    private async Task RunQueueAsync()
    {
        if (_isQueueRunning)
            return;

        var pendingItems = _queue
            .Where(item => item.Job.Status is CopyJobStatus.Ready or CopyJobStatus.Paused)
            .ToList();
        if (pendingItems.Count == 0)
            return;

        using var sleepGuard = PowerManagementService.PreventSleep();
        _isQueueRunning = true;
        _pauseRequested = false;
        SetRunningState(true);
        _copyCancellation = new CancellationTokenSource();

        foreach (var item in pendingItems)
        {
            _activeQueueItem = item;
            item.Job.ActivePerformanceMode = SystemActivityService.Resolve(item.Job.RequestedPerformanceMode);
            StatusText.Text = T($"Kopyalanıyor: {item.Title}", $"Copying: {item.Title}") +
                              $" • {PerformanceModeLabel(item.Job.ActivePerformanceMode)}";
            CurrentFileText.Text = item.Paths;
            item.SetRunning();
            item.Job.Status = CopyJobStatus.Running;
            await SaveQueueStateAsync();
            var progress = new Progress<RobocopyProgress>(UpdateProgress);
            var logLines = new ConcurrentQueue<string>();
            RobocopyResult result;
            if (_settings.WaitForNetwork && IsNetworkJob(item.Job)
                && !await WaitForNetworkAsync(item.Job, _copyCancellation.Token))
            {
                result = new RobocopyResult(16, CopyJobStatus.Failed,
                    T("Ağ bağlantısı belirtilen süre içinde geri gelmedi.",
                        "The network connection did not return within the configured time."));
            }
            else
            {
                result = await _runner.RunAsync(
                    item.Job,
                    progress,
                    _copyCancellation.Token,
                    logLines.Enqueue);
                if (_settings.WaitForNetwork && IsNetworkJob(item.Job) && IsLikelyNetworkFailure(result)
                    && await WaitForNetworkAsync(item.Job, _copyCancellation.Token))
                {
                    logLines.Enqueue(T("--- Ağ bağlantısı geri geldi; yeniden başlatılabilir transfer sürdürülüyor. ---",
                        "--- Network connection restored; resuming the restartable transfer. ---"));
                    result = await _runner.RunAsync(
                        item.Job, progress, _copyCancellation.Token, logLines.Enqueue);
                }
            }
            if (_pauseRequested && result.Status == CopyJobStatus.Cancelled)
            {
                result = new RobocopyResult(-1, CopyJobStatus.Paused,
                    T("Transfer duraklatıldı; devam komutunda kaldığı yerden sürdürülecek.",
                        "Transfer paused; it will continue from where it stopped when resumed."));
            }
            if (result.Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings
                && item.Job.Options.Verification != VerificationMode.None)
            {
                try
                {
                    StatusText.Text = T($"Doğrulanıyor: {item.Title}", $"Verifying: {item.Title}");
                    CopyProgressBar.IsIndeterminate = true;
                    PercentageText.Text = "—";
                    item.SetVerifying();
                    var verificationProgress = new Progress<CopyVerificationProgress>(value =>
                    {
                        CurrentFileText.Text = value.RelativePath;
                        item.SetVerifying(value.RelativePath);
                    });
                    var verification = await _verificationService.VerifyAsync(
                        item.Job,
                        verificationProgress,
                        _copyCancellation.Token);
                    result = verification.IsSuccessful
                        ? result with { Summary = $"{result.Summary} {verification.Summary}" }
                        : new RobocopyResult(result.ExitCode, CopyJobStatus.Failed,
                            $"{result.Summary} {verification.Summary}");
                }
                catch (OperationCanceledException)
                {
                    result = _pauseRequested
                        ? new RobocopyResult(-1, CopyJobStatus.Paused,
                            T("Doğrulama duraklatıldı; transfer yeniden doğrulanabilir.",
                                "Verification paused; the transfer can be verified again."))
                        : new RobocopyResult(-1, CopyJobStatus.Cancelled,
                            T("Doğrulama kullanıcı tarafından iptal edildi.",
                                "Verification was cancelled by the user."));
                }
            }

            item.Job.ExitCode = result.ExitCode;
            item.Job.Status = result.Status;
            item.Job.Summary = result.Summary;
            item.Job.FailedItemCount = result.FailedItemCount;
            item.Job.Failures = result.Failures?.ToList() ?? [];
            item.Job.CompletedAt = DateTimeOffset.Now;
            if (result.Status == CopyJobStatus.Paused)
                item.SetPaused();
            else
                item.SetResult(result with { Summary = LocalizeSummary(result.Summary) });
            try
            {
                item.Job.LogPath = await _jobLogStore.SaveAsync(item.Job, logLines);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                item.Job.Summary += " Günlük kaydedilemedi: " + ex.Message;
            }
            try
            {
                await _historyStore.AddAsync(item.Job);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                item.Job.Summary += " Geçmiş kaydedilemedi: " + ex.Message;
            }
            ShowResult(item.Job);
            await SaveQueueStateAsync();

            if (result.Status is CopyJobStatus.Cancelled or CopyJobStatus.Paused)
                break;
            if (result.Status == CopyJobStatus.Failed && !_settings.ContinueQueueOnError)
                break;
        }

        _activeQueueItem = null;
        _isQueueRunning = false;
        SetRunningState(false);
        UpdateQueueState();
        var completedCount = pendingItems.Count(item => item.Job.Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings);
        var partialCount = pendingItems.Count(item => item.Job.Status == CopyJobStatus.CompletedWithErrors);
        var failedCount = pendingItems.Count(item => item.Job.Status == CopyJobStatus.Failed);
        if (!_pauseRequested && _settings.NotificationsEnabled)
            _notificationService.ShowTransferSummary(completedCount, partialCount, failedCount);
        _copyCancellation?.Dispose();
        _copyCancellation = null;
        await SaveQueueStateAsync();
        sleepGuard.Dispose();
        if (!_pauseRequested && partialCount == 0 && failedCount == 0
            && _settings.CompletionAction != CompletionAction.None)
        {
            try { PowerManagementService.Execute(_settings.CompletionAction); }
            catch (InvalidOperationException ex)
            {
                ValidationText.Text = ex.Message;
                ValidationInfoBar.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateProgress(RobocopyProgress progress)
    {
        CurrentFileText.Text = progress.Message;
        _activeQueueItem?.SetRunning(progress.Percentage, progress.Message);
        if (progress.Percentage is not { } percentage)
            return;

        CopyProgressBar.IsIndeterminate = false;
        CopyProgressBar.Value = percentage;
        PercentageText.Text = $"{percentage:0}%";
        if (progress.BytesPerSecond is > 0)
            SpeedText.Text = T($"Hız: {QueueItemViewModel.FormatBytes((long)progress.BytesPerSecond)}/sn",
                $"Speed: {QueueItemViewModel.FormatBytes((long)progress.BytesPerSecond)}/s");
        if (progress.EstimatedRemaining is { } remaining)
            RemainingText.Text = T($"Kalan: {FormatDuration(remaining)}", $"Remaining: {FormatDuration(remaining)}");
        if (progress.CompletedFiles is { } completedFiles)
            CompletedFilesText.Text = T($"{completedFiles:N0} dosya", $"{completedFiles:N0} files");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _pauseRequested = false;
        StatusText.Text = T("İptal ediliyor…", "Cancelling…");
        CancelButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        _copyCancellation?.Cancel();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isQueueRunning)
            return;
        _pauseRequested = true;
        StatusText.Text = T("Duraklatılıyor…", "Pausing…");
        PauseButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        _copyCancellation?.Cancel();
    }

    private async void ClearQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning)
            return;
        _queue.Clear();
        UpdateQueueState();
        await SaveQueueStateAsync();
    }

    private void QueueListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateQueueState();

    private void MoveUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedQueueItem(-1);

    private void MoveDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedQueueItem(1);

    private void MoveSelectedQueueItem(int offset)
    {
        if (_isQueueRunning || QueueListView.SelectedItem is not QueueItemViewModel item)
            return;
        var currentIndex = _queue.IndexOf(item);
        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= _queue.Count)
            return;
        _queue.Move(currentIndex, targetIndex);
        QueueListView.SelectedItem = item;
        UpdateQueueState();
        _ = SaveQueueStateAsync();
    }

    private async void RemoveQueueItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListView.SelectedItem is not QueueItemViewModel item)
            return;
        _queue.Remove(item);
        UpdateQueueState();
        await SaveQueueStateAsync();
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListView.SelectedItem is not QueueItemViewModel item)
            return;
        if (item.Job.Status is CopyJobStatus.CompletedWithErrors
            or CopyJobStatus.Failed or CopyJobStatus.Cancelled or CopyJobStatus.Paused)
            item.ResetForRetry();
        UpdateQueueState();
        await SaveQueueStateAsync();
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = T("Kaynak klasör olarak seç", "Use as source folder");
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private void ExplorerIntegrationButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _explorerIntegration.SetEnabled(!_explorerIntegration.IsRegistered);
            UpdateIntegrationState();
        }
        catch (Exception ex)
        {
            ValidationText.Text = T("Explorer entegrasyonu güncellenemedi: ", "Could not update Explorer integration: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_notificationService.ShowTestNotification())
        {
            ValidationText.Text = T("Windows bildirimi gösterilemedi.", "Could not show the Windows notification.");
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private async void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await _diagnosticsService.CreatePackageAsync();
            PreflightText.Text = T($"Tanılama paketi oluşturuldu: {path}", $"Diagnostics package created: {path}");
            PreflightInfoBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = T("Tanılama paketi oluşturulamadı: ", "Could not create diagnostics package: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    public void RestoreFromTray() => _trayIcon.Restore();

    public async Task HandleShellRequestAsync(ShellLaunchRequest request)
    {
        RestoreFromTray();
        if (request.ScheduledJob is not null)
            LoadJobIntoForm(request.ScheduledJob);
        else
        {
            if (!string.IsNullOrWhiteSpace(request.SourcePath))
                SourceTextBox.Text = request.SourcePath;
            if (!string.IsNullOrWhiteSpace(request.DestinationPath))
                DestinationTextBox.Text = request.DestinationPath;
        }
        if (!request.AutoStart)
            return;
        if (request.SourcePaths is { Count: > 1 } sources
            && !string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            foreach (var source in sources)
            {
                SourceTextBox.Text = source;
                DestinationTextBox.Text = request.DestinationPath;
                await AddCurrentTransferToQueueAsync();
            }
        }
        else
        {
            await AddCurrentTransferToQueueAsync();
        }
        await RunQueueAsync();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isQueueRunning || _closeConfirmed)
            return;

        if (_settings.MinimizeToTrayWhileRunning)
        {
            args.Cancel = true;
            _trayIcon.HideToTray();
        }
        else
        {
            args.Cancel = true;
            if (!_closeDialogOpen)
                _ = ConfirmCloseDuringTransferAsync();
        }
    }

    private async Task ConfirmCloseDuringTransferAsync()
    {
        _closeDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = T("Aktif transfer devam ediyor", "A transfer is still active"),
                Content = T("Çıkarsanız aktif iş güvenli biçimde iptal edilir. İsterseniz uygulamayı tepsiye küçülterek devam edebilirsiniz.",
                    "Closing will safely cancel the active job. You can minimize to the tray and keep it running instead."),
                PrimaryButtonText = T("İptal et ve çık", "Cancel and exit"),
                SecondaryButtonText = T("Tepsiye küçült", "Minimize to tray"),
                CloseButtonText = T("Vazgeç", "Keep open"),
                XamlRoot = Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                _trayIcon.HideToTray();
                return;
            }
            if (result != ContentDialogResult.Primary)
                return;
            _closeConfirmed = true;
            _pauseRequested = false;
            _copyCancellation?.Cancel();
            Close();
        }
        finally { _closeDialogOpen = false; }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _appWindow.Closing -= AppWindow_Closing;
        _trayIcon.Dispose();
    }

    private void UpdateIntegrationState()
    {
        var explorerText = _explorerIntegration.IsRegistered
            ? T("Kopyala/yapıştır menüleri etkin; Windows 11’de ‘Daha fazla seçenek göster’ altında görünür.",
                "Copy/paste menus are enabled; on Windows 11 they appear under ‘Show more options’.")
            : T("Explorer kopyala/yapıştır menüleri henüz etkin değil.",
                "Explorer copy/paste menus are not enabled yet.");
        var notificationText = _notificationService.IsAvailable
            ? T("Windows bildirimleri hazır.", "Windows notifications are ready.")
            : T("Windows bildirimleri bu oturumda kullanılamıyor.", "Windows notifications are unavailable in this session.");
        IntegrationStatusText.Text = explorerText + " " + notificationText;
        ExplorerIntegrationButton.Content = _explorerIntegration.IsRegistered
            ? T("Sağ tık menüsünü kaldır", "Remove context menu")
            : T("Sağ tık menüsünü ekle", "Add context menu");
        TestNotificationButton.IsEnabled = _notificationService.IsAvailable;
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.FirstOrDefault(item => item is StorageFolder) is StorageFolder folder)
        {
            SourceTextBox.Text = folder.Path;
            DropHintText.Text = T($"Kaynak seçildi: {folder.Name}", $"Source selected: {folder.Name}");
            PreflightInfoBar.Visibility = Visibility.Collapsed;
            ValidationInfoBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var jobs = await _historyStore.LoadAsync();
        var choices = jobs.Select(job => new HistoryChoice(
            job,
            $"{GetStatusLabel(job.Status)} • {job.CreatedAt:g}\n{job.SourcePath} → {job.DestinationPath}"))
            .ToList();
        var list = new ListView
        {
            ItemsSource = choices,
            DisplayMemberPath = nameof(HistoryChoice.Label),
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 390,
            MinWidth = 620
        };
        if (choices.Count > 0)
            list.SelectedIndex = 0;
        var clearButton = new Button
        {
            Content = T("Geçmişi temizle", "Clear history"),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = choices.Count > 0
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(choices.Count == 0
            ? new TextBlock { Text = T("Henüz kayıtlı transfer bulunmuyor.", "There are no saved transfers yet.") }
            : list);
        panel.Children.Add(clearButton);

        var dialog = new ContentDialog
        {
            Title = T("Transfer geçmişi", "Transfer history"),
            Content = panel,
            PrimaryButtonText = T("Forma yükle", "Load into form"),
            SecondaryButtonText = T("Günlüğü aç", "Open log"),
            CloseButtonText = T("Kapat", "Close"),
            XamlRoot = Content.XamlRoot
        };
        clearButton.Click += async (_, _) =>
        {
            await _historyStore.ClearAsync();
            choices.Clear();
            list.ItemsSource = null;
            clearButton.IsEnabled = false;
            LastJobTitle.Text = T("Henüz tamamlanan işlem yok", "No completed operation yet");
            LastJobDetails.Text = T("İlk transferiniz burada görünecek.", "Your first transfer will appear here.");
        };
        var result = await dialog.ShowAsync();
        if (list.SelectedItem is not HistoryChoice selected)
            return;
        if (result == ContentDialogResult.Primary)
            LoadJobIntoForm(selected.Job);
        else if (result == ContentDialogResult.Secondary)
            OpenLog(selected.Job.LogPath);
    }

    private async void ScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        var schedules = (await _scheduleStore.LoadAsync()).ToList();
        var choices = schedules.Select(schedule => new ScheduleChoice(
            schedule,
            $"{schedule.Name} • {schedule.TimeOfDay}\n{schedule.Job.SourcePath} → {schedule.Job.DestinationPath}"))
            .ToList();
        var list = new ListView
        {
            ItemsSource = choices,
            DisplayMemberPath = nameof(ScheduleChoice.Label),
            SelectionMode = ListViewSelectionMode.Single,
            MinWidth = 620,
            MaxHeight = 220
        };
        if (choices.Count > 0)
            list.SelectedIndex = 0;
        var nameBox = new TextBox
        {
            Header = T("Görev adı", "Task name"),
            Text = T("Günlük CopyPaste transferi", "Daily CopyPaste transfer"),
            PlaceholderText = T("Örn. Gece NAS yedeği", "Example: Nightly NAS backup")
        };
        var timePicker = new TimePicker
        {
            Header = T("Çalışma saati", "Run at"),
            Time = new TimeSpan(2, 0, 0),
            ClockIdentifier = "24HourClock"
        };
        var frequency = new ComboBox
        {
            Header = T("Zamanlama türü", "Schedule type"),
            DisplayMemberPath = "Label",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                new OptionChoice<ScheduleKind>(ScheduleKind.Daily, T("Her gün", "Daily")),
                new OptionChoice<ScheduleKind>(ScheduleKind.Weekly, T("Her hafta", "Weekly")),
                new OptionChoice<ScheduleKind>(ScheduleKind.Once, T("Tek sefer", "Once")),
                new OptionChoice<ScheduleKind>(ScheduleKind.WhenIdle, T("Bilgisayar boşta olduğunda", "When the computer is idle"))
            },
            SelectedIndex = 0
        };
        var dayOfWeek = new ComboBox
        {
            Header = T("Haftanın günü", "Day of week"),
            DisplayMemberPath = "Label",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = Enum.GetValues<DayOfWeek>().Select(day => new OptionChoice<DayOfWeek>(day,
                T(day switch
                {
                    DayOfWeek.Monday => "Pazartesi", DayOfWeek.Tuesday => "Salı", DayOfWeek.Wednesday => "Çarşamba",
                    DayOfWeek.Thursday => "Perşembe", DayOfWeek.Friday => "Cuma", DayOfWeek.Saturday => "Cumartesi",
                    _ => "Pazar"
                }, day.ToString()))).ToArray(),
            SelectedIndex = 1,
            Visibility = Visibility.Collapsed
        };
        var runDate = new CalendarDatePicker
        {
            Header = T("Çalışma tarihi", "Run date"),
            Date = DateTimeOffset.Now.AddDays(1),
            MinDate = DateTimeOffset.Now.Date,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        var idleMinutes = new NumberBox
        {
            Header = T("Boşta kalma süresi (dakika)", "Idle time (minutes)"),
            Minimum = 1,
            Maximum = 999,
            Value = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        frequency.SelectionChanged += (_, _) =>
        {
            var kind = (frequency.SelectedItem as OptionChoice<ScheduleKind>)?.Value ?? ScheduleKind.Daily;
            dayOfWeek.Visibility = kind == ScheduleKind.Weekly ? Visibility.Visible : Visibility.Collapsed;
            runDate.Visibility = kind == ScheduleKind.Once ? Visibility.Visible : Visibility.Collapsed;
            idleMinutes.Visibility = kind == ScheduleKind.WhenIdle ? Visibility.Visible : Visibility.Collapsed;
            timePicker.Visibility = kind == ScheduleKind.WhenIdle ? Visibility.Collapsed : Visibility.Visible;
        };
        var panel = new StackPanel { Spacing = 10 };
        if (choices.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = T("Kayıtlı görevler", "Saved tasks"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(list);
        }
        panel.Children.Add(nameBox);
        panel.Children.Add(frequency);
        panel.Children.Add(dayOfWeek);
        panel.Children.Add(runDate);
        panel.Children.Add(idleMinutes);
        panel.Children.Add(timePicker);
        var dialog = new ContentDialog
        {
            Title = T("Zamanlanmış transferler", "Scheduled transfers"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 590, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = T("Görev oluştur", "Create task"),
            SecondaryButtonText = choices.Count > 0 ? T("Seçili görevi kaldır", "Remove selected task") : string.Empty,
            CloseButtonText = T("Kapat", "Close"),
            XamlRoot = Content.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary && list.SelectedItem is ScheduleChoice selected)
        {
            try { await _taskScheduler.RemoveAsync(selected.Schedule.Id); }
            catch (InvalidOperationException) { }
            await _scheduleStore.RemoveAsync(selected.Schedule.Id);
            PreflightText.Text = T("Zamanlanmış görev kaldırıldı.", "Scheduled task removed.");
            PreflightInfoBar.Visibility = Visibility.Visible;
            return;
        }
        if (result != ContentDialogResult.Primary)
            return;

        var validation = CopyJobValidator.Validate(SourceTextBox.Text, DestinationTextBox.Text);
        if (!validation.IsValid
            || ProfileComboBox.SelectedItem is not CopyProfile profile
            || ExistingFileBehaviorComboBox.SelectedItem is not OptionChoice<ExistingFileBehavior> existingChoice
            || VerificationModeComboBox.SelectedItem is not OptionChoice<VerificationMode> verificationChoice)
        {
            ValidationText.Text = validation.Error ?? T("Zamanlama ayarları geçersiz.", "Scheduling settings are invalid.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            return;
        }
        var options = CopyJobOptionsParser.Parse(existingChoice.Value, verificationChoice.Value,
            FilePatternsTextBox.Text, ExcludedDirectoriesTextBox.Text);
        if (!options.IsValid)
        {
            ValidationText.Text = options.Error;
            ValidationInfoBar.Visibility = Visibility.Visible;
            return;
        }

        var schedule = new ScheduledTransfer
        {
            Name = string.IsNullOrWhiteSpace(nameBox.Text) ? T("CopyPaste transferi", "CopyPaste transfer") : nameBox.Text.Trim(),
            TimeOfDay = timePicker.Time.ToString(@"hh\:mm"),
            Kind = (frequency.SelectedItem as OptionChoice<ScheduleKind>)?.Value ?? ScheduleKind.Daily,
            DayOfWeek = (dayOfWeek.SelectedItem as OptionChoice<DayOfWeek>)?.Value ?? DayOfWeek.Monday,
            RunDate = runDate.Date is { } selectedDate ? DateOnly.FromDateTime(selectedDate.DateTime) : null,
            IdleMinutes = double.IsNaN(idleMinutes.Value) ? 10 : (int)Math.Clamp(idleMinutes.Value, 1, 999),
            Job = new CopyJob
            {
                SourcePath = Path.GetFullPath(SourceTextBox.Text.Trim()),
                DestinationPath = CopyDestinationResolver.Resolve(
                    SourceTextBox.Text, DestinationTextBox.Text,
                    (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
                        ?? CopyRootMode.SelectedFolder),
                DestinationRootPath = Path.GetFullPath(DestinationTextBox.Text.Trim()),
                RootMode = (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
                    ?? CopyRootMode.SelectedFolder,
                Profile = profile,
                RequestedPerformanceMode = _settings.PerformanceMode,
                BandwidthLimitMbps = _settings.BandwidthLimitMbps,
                UseBackupMode = _useBackupModeForSource,
                Options = options.Options!
            }
        };
        try
        {
            await _taskScheduler.RegisterAsync(schedule);
            await _scheduleStore.SaveAsync(schedule);
            PreflightText.Text = T($"{schedule.Name} Windows Görev Zamanlayıcı'ya kaydedildi.",
                $"{schedule.Name} was registered with Windows Task Scheduler.");
            PreflightInfoBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = T("Zamanlanmış görev oluşturulamadı: ", "Could not create scheduled task: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsDialogOpen)
            return;

        _settingsDialogOpen = true;
        try
        {
            var language = new ComboBox
            {
                Header = T("Dil", "Language"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    new OptionChoice<string>("tr-TR", T("Türkçe", "Turkish")),
                    new OptionChoice<string>("en-US", T("İngilizce", "English"))
                },
                DisplayMemberPath = "Label"
            };
            language.SelectedItem = language.Items.Cast<object>()
                .OfType<OptionChoice<string>>()
                .First(choice => choice.Value.Equals(_settings.Language, StringComparison.OrdinalIgnoreCase));

            var performance = new ComboBox
            {
                Header = T("Performans modu", "Performance mode"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                DisplayMemberPath = "Label",
                ItemsSource = new[]
                {
                    new OptionChoice<TransferPerformanceMode>(TransferPerformanceMode.Automatic,
                        T("Otomatik — oyunda sessiz, boşta tam hız", "Automatic — quiet in games, full speed when idle")),
                    new OptionChoice<TransferPerformanceMode>(TransferPerformanceMode.FullSpeed,
                        T("Tam hız", "Full speed")),
                    new OptionChoice<TransferPerformanceMode>(TransferPerformanceMode.Balanced,
                        T("Dengeli", "Balanced")),
                    new OptionChoice<TransferPerformanceMode>(TransferPerformanceMode.LowResource,
                        T("Düşük kaynak", "Low resource"))
                }
            };
            performance.SelectedItem = performance.Items.Cast<object>()
                .OfType<OptionChoice<TransferPerformanceMode>>()
                .First(choice => choice.Value == _settings.PerformanceMode);

            var notifications = new ToggleSwitch
            {
                Header = T("Transfer sonunda bildirim göster", "Show a notification when a transfer finishes"),
                IsOn = _settings.NotificationsEnabled
            };
            var minimizeOnClose = new ToggleSwitch
            {
                Header = T("Aktif işte kapatılırsa tepsiye küçült", "Minimize to tray when closed during a transfer"),
                IsOn = _settings.MinimizeToTrayWhileRunning
            };
            var autoUpdates = new ToggleSwitch
            {
                Header = T("Güncellemeleri güvenle arka planda indir", "Securely download updates in the background"),
                IsOn = _settings.AutoDownloadUpdates
            };
            var checkUpdatesOnStartup = new ToggleSwitch
            {
                Header = T("Açılışta güncellemeleri kontrol et", "Check for updates at startup"),
                IsOn = _settings.CheckForUpdatesOnStartup
            };
            var startWithWindows = new ToggleSwitch
            {
                Header = T("Windows açıldığında CopyPaste'i başlat", "Start CopyPaste when Windows starts"),
                IsOn = _settings.StartWithWindows
            };
            var startMinimized = new ToggleSwitch
            {
                Header = T("Windows başlangıcında tepside aç", "Start in the system tray with Windows"),
                IsOn = _settings.StartMinimizedWithWindows,
                IsEnabled = _settings.StartWithWindows
            };
            startWithWindows.Toggled += (_, _) => startMinimized.IsEnabled = startWithWindows.IsOn;
            var bandwidthLimit = new NumberBox
            {
                Header = T("Hız sınırı (MB/sn, 0 = sınırsız)", "Speed limit (MB/s, 0 = unlimited)"),
                Minimum = 0,
                Maximum = 10240,
                Value = _settings.BandwidthLimitMbps,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var completionAction = new ComboBox
            {
                Header = T("Kuyruk tamamlandığında", "When the queue finishes"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                DisplayMemberPath = "Label",
                ItemsSource = new[]
                {
                    new OptionChoice<CompletionAction>(CompletionAction.None, T("Bir şey yapma", "Do nothing")),
                    new OptionChoice<CompletionAction>(CompletionAction.Sleep, T("Bilgisayarı uyut", "Put the computer to sleep")),
                    new OptionChoice<CompletionAction>(CompletionAction.ShutDown, T("Bilgisayarı kapat", "Shut down the computer"))
                }
            };
            completionAction.SelectedItem = completionAction.Items.Cast<object>()
                .OfType<OptionChoice<CompletionAction>>()
                .First(choice => choice.Value == _settings.CompletionAction);
            var panel = new StackPanel { Spacing = 14, MinWidth = 430 };
            panel.Children.Add(language);
            panel.Children.Add(performance);
            panel.Children.Add(notifications);
            panel.Children.Add(minimizeOnClose);
            panel.Children.Add(autoUpdates);
            panel.Children.Add(checkUpdatesOnStartup);
            panel.Children.Add(startWithWindows);
            panel.Children.Add(startMinimized);
            panel.Children.Add(bandwidthLimit);
            panel.Children.Add(completionAction);

            UpdateIntegrationState();
            var integrationTitle = new TextBlock
            {
                Text = T("Windows entegrasyonu", "Windows integration"),
                FontSize = 16,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var integrationStatus = new TextBlock
            {
                Text = IntegrationStatusText.Text,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72
            };
            var explorerIntegration = new Button
            {
                Content = ExplorerIntegrationButton.Content,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            explorerIntegration.Click += (_, args) =>
            {
                ExplorerIntegrationButton_Click(explorerIntegration, args);
                integrationStatus.Text = IntegrationStatusText.Text;
                explorerIntegration.Content = ExplorerIntegrationButton.Content;
            };
            var testNotification = new Button
            {
                Content = T("Bildirimi test et", "Test notification"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            testNotification.Click += (_, args) => TestNotificationButton_Click(testNotification, args);
            var diagnostics = new Button
            {
                Content = T("Tanılama paketi oluştur", "Create diagnostics package"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            diagnostics.Click += (_, args) => DiagnosticsButton_Click(diagnostics, args);
            panel.Children.Add(integrationTitle);
            panel.Children.Add(integrationStatus);
            panel.Children.Add(explorerIntegration);
            panel.Children.Add(testNotification);
            panel.Children.Add(diagnostics);

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = T("Ayarlar", "Settings"),
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 560,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                PrimaryButtonText = T("Kaydet", "Save"),
                CloseButtonText = T("İptal", "Cancel"),
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            _settings = _settings with
            {
                Language = (language.SelectedItem as OptionChoice<string>)?.Value ?? _settings.Language,
                PerformanceMode = (performance.SelectedItem as OptionChoice<TransferPerformanceMode>)?.Value
                    ?? _settings.PerformanceMode,
                NotificationsEnabled = notifications.IsOn,
                MinimizeToTrayWhileRunning = minimizeOnClose.IsOn,
                AutoDownloadUpdates = autoUpdates.IsOn,
                CheckForUpdatesOnStartup = checkUpdatesOnStartup.IsOn,
                StartWithWindows = startWithWindows.IsOn,
                StartMinimizedWithWindows = startWithWindows.IsOn && startMinimized.IsOn,
                BandwidthLimitMbps = double.IsNaN(bandwidthLimit.Value)
                    ? 0
                    : (int)Math.Clamp(bandwidthLimit.Value, 0, 10240),
                CompletionAction = (completionAction.SelectedItem as OptionChoice<CompletionAction>)?.Value
                    ?? CompletionAction.None
            };
            ApplySettingsToUi(_settings);
            await _settingsStore.SaveAsync(_settings);
            ApplyStartWithWindowsSetting(_settings.StartWithWindows, _settings.StartMinimizedWithWindows);
            ApplyLanguage(_settings.Language);
            PreflightText.Text = T("Çalışma ayarları kaydedildi.", "Application settings saved.");
            PreflightInfoBar.Visibility = Visibility.Visible;
        }
        finally
        {
            _settingsDialogOpen = false;
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = GetSettingsFromUi();
        await _settingsStore.SaveAsync(_settings);
        ApplyLanguage(_settings.Language);
        PreflightText.Text = T("Çalışma ayarları kaydedildi.", "Application settings saved.");
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        _language = _settings.Language;
        _profiles.Clear();
        foreach (var profile in GetBuiltInProfiles().Concat(_settings.CustomProfiles)
                     .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
            _profiles.Add(profile);
        ApplySettingsToUi(_settings);
        ApplyLanguage(_settings.Language);
    }

    private AppSettings GetSettingsFromUi() => new()
    {
        DefaultProfileId = (ProfileComboBox.SelectedItem as CopyProfile)?.Id ?? "balanced",
        ExistingFiles = (ExistingFileBehaviorComboBox.SelectedItem as OptionChoice<ExistingFileBehavior>)?.Value
            ?? ExistingFileBehavior.Update,
        Verification = (VerificationModeComboBox.SelectedItem as OptionChoice<VerificationMode>)?.Value
            ?? VerificationMode.Size,
        CopyRootMode = (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
            ?? CopyRootMode.SelectedFolder,
        FilePatterns = FilePatternsTextBox.Text,
        ExcludedDirectories = ExcludedDirectoriesTextBox.Text,
        ContinueQueueOnError = ContinueOnErrorToggle.IsOn,
        NotificationsEnabled = _settings.NotificationsEnabled,
        MinimizeToTrayWhileRunning = _settings.MinimizeToTrayWhileRunning,
        AutoDownloadUpdates = _settings.AutoDownloadUpdates,
        CheckForUpdatesOnStartup = _settings.CheckForUpdatesOnStartup,
        StartWithWindows = _settings.StartWithWindows,
        StartMinimizedWithWindows = _settings.StartMinimizedWithWindows,
        PerformanceMode = _settings.PerformanceMode,
        BandwidthLimitMbps = _settings.BandwidthLimitMbps,
        CompletionAction = _settings.CompletionAction,
        WaitForNetwork = WaitForNetworkToggle.IsOn,
        NetworkRetryMinutes = double.IsNaN(NetworkRetryMinutesBox.Value)
            ? 15
            : (int)Math.Clamp(NetworkRetryMinutesBox.Value, 1, 1440),
        Language = _settings.Language,
        FavoriteLocations = _settings.FavoriteLocations,
        RecentSources = _settings.RecentSources,
        RecentDestinations = _settings.RecentDestinations,
        CustomProfiles = _settings.CustomProfiles
    };

    private void ApplySettingsToUi(AppSettings settings)
    {
        ProfileComboBox.SelectedItem = _profiles.FirstOrDefault(profile => profile.Id == settings.DefaultProfileId)
            ?? _profiles[0];
        SelectChoice(ExistingFileBehaviorComboBox, settings.ExistingFiles);
        SelectChoice(VerificationModeComboBox, settings.Verification);
        SelectChoice(CopyRootModeComboBox, settings.CopyRootMode);
        FilePatternsTextBox.Text = string.IsNullOrWhiteSpace(settings.FilePatterns) ? "*" : settings.FilePatterns;
        ExcludedDirectoriesTextBox.Text = settings.ExcludedDirectories;
        ContinueOnErrorToggle.IsOn = settings.ContinueQueueOnError;
        WaitForNetworkToggle.IsOn = settings.WaitForNetwork;
        NetworkRetryMinutesBox.Value = Math.Clamp(settings.NetworkRetryMinutes, 1, 1440);
    }

    private static void SelectChoice<T>(ComboBox comboBox, T value) where T : struct, Enum
    {
        comboBox.SelectedItem = comboBox.Items.Cast<object>()
            .OfType<OptionChoice<T>>()
            .FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, value));
    }

    private void ApplyLanguage(string language)
    {
        var selectedProfileId = (ProfileComboBox.SelectedItem as CopyProfile)?.Id ?? _settings.DefaultProfileId;
        _language = LocalizationService.IsEnglish(language) ? "en-US" : "tr-TR";
        _profiles.Clear();
        foreach (var profile in GetBuiltInProfiles().Concat(_settings.CustomProfiles)
                     .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
            _profiles.Add(profile);
        ProfileComboBox.SelectedItem = _profiles.FirstOrDefault(profile => profile.Id == selectedProfileId) ?? _profiles[0];
        ApplyLanguageChoices();
        LocalizationService.Apply(Content, _language);
        CheckUpdatesButton.Content = T("Güncellemeleri kontrol et", "Check for updates");
        foreach (var item in _queue)
            item.SetLanguage(LocalizationService.IsEnglish(_language));
        UpdateProfileDescription();
        UpdateQueueState();
        UpdateIntegrationState();
    }

    private void ApplyLanguageChoices()
    {
        var existing = (ExistingFileBehaviorComboBox.SelectedItem as OptionChoice<ExistingFileBehavior>)?.Value
            ?? ExistingFileBehavior.Update;
        ExistingFileBehaviorComboBox.ItemsSource = new[]
        {
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Update,
                T("Yalnızca gerekenleri güncelle", "Update only changed files")),
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Skip,
                T("Hedefte bulunanları atla", "Skip files already at destination")),
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Overwrite,
                T("Tüm dosyaların üzerine yaz", "Overwrite all files"))
        };
        SelectChoice(ExistingFileBehaviorComboBox, existing);

        var verification = (VerificationModeComboBox.SelectedItem as OptionChoice<VerificationMode>)?.Value
            ?? VerificationMode.Size;
        VerificationModeComboBox.ItemsSource = new[]
        {
            new OptionChoice<VerificationMode>(VerificationMode.Size, T("Hızlı — dosya boyutu", "Fast — file size")),
            new OptionChoice<VerificationMode>(VerificationMode.Sha256, T("Tam — SHA-256", "Full — SHA-256")),
            new OptionChoice<VerificationMode>(VerificationMode.None, T("Doğrulama yapma", "Do not verify"))
        };
        SelectChoice(VerificationModeComboBox, verification);

        var rootMode = (CopyRootModeComboBox.SelectedItem as OptionChoice<CopyRootMode>)?.Value
            ?? _settings.CopyRootMode;
        CopyRootModeComboBox.ItemsSource = new[]
        {
            new OptionChoice<CopyRootMode>(CopyRootMode.SelectedFolder,
                T("Seçilen klasörü kopyala (önerilen)", "Copy the selected folder (recommended)")),
            new OptionChoice<CopyRootMode>(CopyRootMode.ContentsOnly,
                T("Yalnızca klasörün içeriğini kopyala", "Copy only the folder contents"))
        };
        SelectChoice(CopyRootModeComboBox, rootMode);

    }

    private static void ApplyStartWithWindowsSetting(bool enabled, bool minimized)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        const string valueName = "CopyPaste";
        if (enabled)
            key.SetValue(valueName, $"\"{Environment.ProcessPath}\"{(minimized ? " --minimized" : string.Empty)}");
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private string PerformanceModeLabel(TransferPerformanceMode mode) => mode switch
    {
        TransferPerformanceMode.FullSpeed => T("Tam hız", "Full speed"),
        TransferPerformanceMode.LowResource => T("Düşük kaynak", "Low resource"),
        _ => T("Dengeli", "Balanced")
    };

    public bool MinimizeToTray() => _trayIcon.HideToTray();

    private IEnumerable<CopyProfile> GetBuiltInProfiles() => LocalizationService.IsEnglish(_language)
        ?
        [
            CopyProfiles.All[0] with { Name = "Balanced", Description = "Balanced speed and reliability for everyday copies" },
            CopyProfiles.All[1] with { Name = "Fastest", Description = "High parallelism for SSDs and fast local disks" },
            CopyProfiles.All[2] with { Name = "Large files", Description = "Optimized for videos, archives, and disk images" }
        ]
        : CopyProfiles.All;

    private string T(string turkish, string english) =>
        LocalizationService.IsEnglish(_language) ? english : turkish;

    private string LocalizeSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary) || !LocalizationService.IsEnglish(_language))
            return summary ?? string.Empty;

        return summary
            .Replace("Hedef zaten güncel; kopyalanacak dosya yok.", "The destination is already up to date; there are no files to copy.")
            .Replace("Tüm dosyalar başarıyla kopyalandı.", "All files were copied successfully.")
            .Replace("İşlem hatalarla birlikte tamamlandı.", "The operation completed with errors.")
            .Replace("kopyalanamadı; diğer dosyalar başarıyla işlendi.", "could not be copied; all other files were processed successfully.")
            .Replace("Robocopy ciddi bir hata nedeniyle transferi tamamlayamadı.", "Robocopy could not complete the transfer because of a serious error.")
            .Replace("Doğrulama başarılı:", "Verification successful:")
            .Replace("Doğrulama başarısız:", "Verification failed:")
            .Replace("dosya kontrol edildi.", "files checked.")
            .Replace("1 files checked.", "1 file checked.")
            .Replace("eksik", "missing")
            .Replace("boyut farkı", "size mismatches")
            .Replace("hash farkı", "hash mismatches")
            .Replace("okuma hatası", "read errors");
    }

    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{value.Minutes:00}:{value.Seconds:00}";

    private static bool IsNetworkJob(CopyJob job) =>
        NetworkAvailabilityService.IsNetworkPath(job.SourcePath)
        || NetworkAvailabilityService.IsNetworkPath(job.DestinationPath);

    private static bool IsLikelyNetworkFailure(RobocopyResult result)
    {
        int[] networkErrorCodes = [53, 64, 67, 121, 1231, 1232];
        return result.Status == CopyJobStatus.Failed
               || result.Failures?.Any(failure => failure.ErrorCode is { } code
                   && networkErrorCodes.Contains(code)) == true;
    }

    private async Task<bool> WaitForNetworkAsync(CopyJob job, CancellationToken cancellationToken)
    {
        var progress = new Progress<NetworkWaitProgress>(value =>
        {
            StatusText.Text = T("Ağ bağlantısı bekleniyor", "Waiting for network");
            CurrentFileText.Text = T(value.Message, "Network location is unavailable; waiting for it to return.");
            RemainingText.Text = T($"Kalan bekleme: {FormatDuration(value.Remaining)}",
                $"Wait remaining: {FormatDuration(value.Remaining)}");
        });
        return await _networkAvailability.WaitForAvailabilityAsync(
            job.SourcePath,
            job.DestinationPath,
            TimeSpan.FromMinutes(Math.Clamp(_settings.NetworkRetryMinutes, 1, 1440)),
            progress,
            cancellationToken);
    }

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        var isSource = (sender as FrameworkElement)?.Tag?.ToString() == "Source";
        var path = (isSource ? SourceTextBox.Text : DestinationTextBox.Text).Trim();
        if (!Directory.Exists(path))
        {
            ValidationText.Text = T("Favoriye eklemek için erişilebilir bir klasör seçin.",
                "Select an accessible folder to add it to favorites.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            return;
        }

        path = Path.GetFullPath(path);
        var favorites = _settings.FavoriteLocations.ToList();
        var existing = favorites.FindIndex(item => item.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            favorites.RemoveAt(existing);
            PreflightText.Text = T("Klasör favorilerden kaldırıldı.", "Folder removed from favorites.");
        }
        else
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            favorites.Add(new(string.IsNullOrWhiteSpace(name) ? path : name, path));
            PreflightText.Text = T("Klasör favorilere eklendi.", "Folder added to favorites.");
        }
        _settings = GetSettingsFromUi() with { FavoriteLocations = favorites };
        await _settingsStore.SaveAsync(_settings);
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private async void SavedLocationsButton_Click(object sender, RoutedEventArgs e)
    {
        var isSource = (sender as FrameworkElement)?.Tag?.ToString() == "Source";
        var recent = isSource ? _settings.RecentSources : _settings.RecentDestinations;
        var choices = _settings.FavoriteLocations
            .Select(item => new SavedLocationChoice(item.Path, $"★ {item.Name}\n{item.Path}"))
            .Concat(recent.Where(path => _settings.FavoriteLocations.All(favorite =>
                    !favorite.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                .Select(path => new SavedLocationChoice(path, $"{T("Son kullanılan", "Recently used")}\n{path}")))
            .ToList();
        if (choices.Count == 0)
        {
            ValidationText.Text = T("Henüz favori veya son kullanılan klasör bulunmuyor.",
                "There are no favorite or recently used folders yet.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            return;
        }

        var list = new ListView
        {
            ItemsSource = choices,
            DisplayMemberPath = nameof(SavedLocationChoice.Label),
            SelectionMode = ListViewSelectionMode.Single,
            MinWidth = 560,
            MaxHeight = 380,
            SelectedIndex = 0
        };
        var dialog = new ContentDialog
        {
            Title = isSource ? T("Kaynak klasörü seç", "Select source folder") : T("Hedef klasörü seç", "Select destination folder"),
            Content = list,
            PrimaryButtonText = T("Seç", "Select"),
            CloseButtonText = T("Kapat", "Close"),
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary
            || list.SelectedItem is not SavedLocationChoice selected)
            return;
        if (isSource)
            SourceTextBox.Text = selected.Path;
        else
            DestinationTextBox.Text = selected.Path;
    }

    private async void SaveCustomProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not CopyProfile selectedProfile)
            return;
        var nameBox = new TextBox { PlaceholderText = T("Örn. NAS yedekleme", "Example: NAS backup"), MinWidth = 360 };
        var dialog = new ContentDialog
        {
            Title = T("Özel profil kaydet", "Save custom profile"),
            Content = nameBox,
            PrimaryButtonText = T("Kaydet", "Save"),
            CloseButtonText = T("Vazgeç", "Cancel"),
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary
            || string.IsNullOrWhiteSpace(nameBox.Text))
            return;
        var profile = selectedProfile with
        {
            Id = "custom-" + Guid.NewGuid().ToString("N"),
            Name = nameBox.Text.Trim(),
            Description = T($"{selectedProfile.Name} temel alınarak oluşturulan özel profil",
                $"Custom profile based on {selectedProfile.Name}")
        };
        _profiles.Add(profile);
        ProfileComboBox.SelectedItem = profile;
        _settings = GetSettingsFromUi() with
        {
            DefaultProfileId = profile.Id,
            CustomProfiles = _settings.CustomProfiles.Append(profile).ToList()
        };
        await _settingsStore.SaveAsync(_settings);
        PreflightText.Text = T($"{profile.Name} profili kaydedildi.", $"Profile {profile.Name} was saved.");
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private static IReadOnlyList<string> RememberPath(IReadOnlyList<string> paths, string path) =>
        paths.Where(existing => !existing.Equals(path, StringComparison.OrdinalIgnoreCase))
            .Prepend(path)
            .Take(12)
            .ToList();

    private void LoadJobIntoForm(CopyJob job)
    {
        SourceTextBox.Text = job.SourcePath;
        _useBackupModeForSource = job.UseBackupMode;
        DestinationTextBox.Text = job.DestinationRootPath ?? job.DestinationPath;
        ProfileComboBox.SelectedItem = _profiles.FirstOrDefault(profile => profile.Id == job.Profile.Id)
            ?? _profiles[0];
        SelectChoice(ExistingFileBehaviorComboBox, job.Options.ExistingFiles);
        SelectChoice(VerificationModeComboBox, job.Options.Verification);
        SelectChoice(CopyRootModeComboBox, job.RootMode);
        FilePatternsTextBox.Text = string.Join(';', job.Options.FilePatterns);
        ExcludedDirectoriesTextBox.Text = string.Join(';', job.Options.ExcludedDirectories);
        PreflightText.Text = T("Geçmişteki transfer forma yüklendi; kontrol edip kuyruğa ekleyebilirsiniz.",
            "The historical transfer was loaded into the form; review it and add it to the queue.");
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private void OpenLog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ValidationText.Text = T("Bu transfer için günlük dosyası bulunamadı.", "No log file was found for this transfer.");
            ValidationInfoBar.Visibility = Visibility.Visible;
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void SetRunningState(bool running)
    {
        SourceTextBox.IsEnabled = !running;
        DestinationTextBox.IsEnabled = !running;
        ProfileComboBox.IsEnabled = !running;
        ExistingFileBehaviorComboBox.IsEnabled = !running;
        VerificationModeComboBox.IsEnabled = !running;
        CopyRootModeComboBox.IsEnabled = !running;
        FilePatternsTextBox.IsEnabled = !running;
        ExcludedDirectoriesTextBox.IsEnabled = !running;
        StartButton.IsEnabled = !running;
        CompareButton.IsEnabled = !running;
        StartQueueButton.IsEnabled = !running;
        ClearQueueButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        PauseButton.IsEnabled = running;
        CancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        if (running)
        {
            ProgressCard.Visibility = Visibility.Visible;
            CopyProgressBar.IsIndeterminate = true;
            CopyProgressBar.Value = 0;
            PercentageText.Text = "0%";
            SpeedText.Text = T("Hız: —", "Speed: —");
            RemainingText.Text = T("Kalan: —", "Remaining: —");
            CompletedFilesText.Text = T("0 dosya", "0 files");
            StatusText.Text = T("Kopyalanıyor", "Copying");
        }
        else
        {
            CopyProgressBar.IsIndeterminate = false;
            PauseButton.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowResult(CopyJob job)
    {
        ProgressCard.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        CopyProgressBar.IsIndeterminate = false;
        var finished = job.Status is CopyJobStatus.Completed
            or CopyJobStatus.CompletedWithWarnings
            or CopyJobStatus.CompletedWithErrors;
        CopyProgressBar.Value = finished ? 100 : 0;
        PercentageText.Text = finished ? "100%" : "—";
        StatusText.Text = job.Status switch
        {
            CopyJobStatus.Completed => T("Transfer tamamlandı", "Transfer completed"),
            CopyJobStatus.CompletedWithWarnings => T("Uyarılarla tamamlandı", "Completed with warnings"),
            CopyJobStatus.CompletedWithErrors => T("İşlem hatalarla birlikte tamamlandı", "Operation completed with errors"),
            CopyJobStatus.Cancelled => T("Transfer iptal edildi", "Transfer cancelled"),
            CopyJobStatus.Paused => T("Transfer duraklatıldı", "Transfer paused"),
            _ => T("Transfer başarısız", "Transfer failed")
        };
        CurrentFileText.Text = LocalizeSummary(job.Summary);
        LastJobTitle.Text = StatusText.Text;
        LastJobDetails.Text = $"{job.SourcePath} → {job.DestinationPath}\n{LocalizeSummary(job.Summary)}";
        ExportReportButton.IsEnabled = true;
        ShowFailureDetails(job);
    }

    private async Task LoadHistoryAsync()
    {
        var jobs = await _historyStore.LoadAsync();
        if (jobs.FirstOrDefault() is not { } job)
            return;

        LastJobTitle.Text = GetStatusLabel(job.Status);
        LastJobDetails.Text = $"{job.SourcePath} → {job.DestinationPath}\n{LocalizeSummary(job.Summary)}";
        ExportReportButton.IsEnabled = true;
        ShowFailureDetails(job);
    }

    private async Task LoadQueueStateAsync()
    {
        var recoveredJobs = await _queueStateStore.LoadAsync();
        var recoveredCount = 0;
        foreach (var job in recoveredJobs)
        {
            if (!Directory.Exists(job.SourcePath))
                continue;
            try
            {
                var preflight = await _preflightAnalyzer.AnalyzeAsync(
                    job.SourcePath, job.DestinationPath, job.Options);
                job.EstimatedTotalBytes = preflight.TotalBytes;
                job.EstimatedFileCount = preflight.FileCount;
                var item = new QueueItemViewModel(job, preflight, LocalizationService.IsEnglish(_language));
                if (job.Status == CopyJobStatus.Paused)
                    item.SetPaused();
                else if (job.Status is CopyJobStatus.CompletedWithErrors
                         or CopyJobStatus.Failed or CopyJobStatus.Cancelled)
                    item.SetResult(new RobocopyResult(
                        job.ExitCode ?? 16, job.Status, job.Summary ?? T("Kurtarılan transfer", "Recovered transfer"),
                        job.Failures, job.FailedItemCount));
                _queue.Add(item);
                recoveredCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Erişilemeyen bir ağ veya çıkarılabilir disk işi diğer kuyruğun açılmasını engellemez.
            }
        }
        if (recoveredCount > 0)
        {
            PreflightText.Text = T($"Önceki oturumdan {recoveredCount:N0} kuyruk işi kurtarıldı.",
                $"Recovered {recoveredCount:N0} queue jobs from the previous session.");
            PreflightInfoBar.Visibility = Visibility.Visible;
            UpdateQueueState();
        }
    }

    private async Task SaveQueueStateAsync()
    {
        try
        {
            await _queueStateStore.SaveAsync(_queue.Select(item => item.Job));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = T("Kuyruk durumu kaydedilemedi: ", "Could not save queue state: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private void UpdateQueueState()
    {
        QueueSummaryText.Text = T($"{_queue.Count:N0} iş", $"{_queue.Count:N0} jobs");
        EmptyQueueText.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var hasRunnableItem = _queue.Any(item => item.Job.Status is CopyJobStatus.Ready or CopyJobStatus.Paused);
        StartQueueButton.IsEnabled = !_isQueueRunning && hasRunnableItem;
        StartQueueButton.Content = _queue.Any(item => item.Job.Status == CopyJobStatus.Paused)
            ? T("Kuyruğa devam et", "Resume queue")
            : T("Kuyruğu başlat", "Start queue");
        ClearQueueButton.IsEnabled = !_isQueueRunning && _queue.Count > 0;
        var selected = QueueListView.SelectedItem as QueueItemViewModel;
        var selectedIndex = selected is null ? -1 : _queue.IndexOf(selected);
        MoveUpButton.IsEnabled = !_isQueueRunning && selectedIndex > 0;
        MoveDownButton.IsEnabled = !_isQueueRunning && selectedIndex >= 0 && selectedIndex < _queue.Count - 1;
        RemoveQueueItemButton.IsEnabled = !_isQueueRunning && selected is not null;
        RetryButton.IsEnabled = !_isQueueRunning
            && selected?.Job.Status is CopyJobStatus.CompletedWithErrors
                or CopyJobStatus.Failed
                or CopyJobStatus.Cancelled
                or CopyJobStatus.Paused;
    }

    private string GetStatusLabel(CopyJobStatus status) => status switch
    {
        CopyJobStatus.Ready => T("Hazır", "Ready"),
        CopyJobStatus.Running => T("Kopyalanıyor", "Copying"),
        CopyJobStatus.Paused => T("Duraklatıldı", "Paused"),
        CopyJobStatus.Completed => T("Tamamlandı", "Completed"),
        CopyJobStatus.CompletedWithWarnings => T("Uyarılarla tamamlandı", "Completed with warnings"),
        CopyJobStatus.CompletedWithErrors => T("Hatalarla tamamlandı", "Completed with errors"),
        CopyJobStatus.Cancelled => T("İptal edildi", "Cancelled"),
        _ => T("Başarısız", "Failed")
    };

    private void ShowFailureDetails(CopyJob job)
    {
        _lastResultJob = job;
        var hasFailures = job.FailedItemCount > 0 || job.Failures.Count > 0;
        FailedItemsCard.Visibility = hasFailures ? Visibility.Visible : Visibility.Collapsed;
        if (!hasFailures)
        {
            FailedItemsListView.ItemsSource = null;
            return;
        }

        FailedItemsCountText.Text = T(
            $"Kopyalanamayan öğeler ({Math.Max(job.FailedItemCount, job.Failures.Count):N0})",
            $"Items not copied ({Math.Max(job.FailedItemCount, job.Failures.Count):N0})");
        FailedItemsListView.ItemsSource = job.Failures;
        OpenFailureLogButton.IsEnabled = !string.IsNullOrWhiteSpace(job.LogPath) && File.Exists(job.LogPath);
    }

    private void CopyFailuresButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResultJob?.Failures.Count is not > 0)
            return;

        var text = string.Join(Environment.NewLine,
            _lastResultJob.Failures.Select(failure =>
                $"{failure.Path}\t{failure.Reason}"));
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        FailureActionText.Text = T("Liste panoya kopyalandı.", "The list was copied to the clipboard.");
    }

    private void OpenFailureLogButton_Click(object sender, RoutedEventArgs e) =>
        OpenLog(_lastResultJob?.LogPath);

    private async void ExportReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResultJob is null)
            return;
        try
        {
            var report = await _reportService.ExportAsync(_lastResultJob);
            FailureActionText.Text = T($"Rapor oluşturuldu: {report.HtmlPath}", $"Report created: {report.HtmlPath}");
            PreflightText.Text = T("HTML raporu ve hata CSV dosyası Belgeler klasörüne kaydedildi.",
                "The HTML report and error CSV were saved to Documents.");
            PreflightInfoBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = T("Rapor oluşturulamadı: ", "Could not create the report: ") + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private async void RetryFailedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResultJob is null || _isQueueRunning)
            return;

        if (_lastResultJob.Failures.Count > 0)
        {
            await RetryOnlyFailedItemsAsync(_lastResultJob);
            return;
        }

        var item = _queue.FirstOrDefault(candidate => candidate.Job.Id == _lastResultJob.Id);
        if (item is null)
        {
            LoadJobIntoForm(_lastResultJob);
            if (!await AddCurrentTransferToQueueAsync())
                return;
            item = _queue[^1];
        }
        else
        {
            item.ResetForRetry();
        }

        QueueListView.SelectedItem = item;
        UpdateQueueState();
        await RunQueueAsync();
    }

    private async Task RetryOnlyFailedItemsAsync(CopyJob job)
    {
        _isQueueRunning = true;
        _pauseRequested = false;
        SetRunningState(true);
        _copyCancellation = new CancellationTokenSource();
        StatusText.Text = T($"{job.Failures.Count:N0} hatalı öğe yeniden deneniyor",
            $"Retrying {job.Failures.Count:N0} failed items");
        var logLines = new ConcurrentQueue<string>();
        var progress = new Progress<RobocopyProgress>(UpdateProgress);
        RobocopyResult result;
        try
        {
            result = await _failedItemRetryService.RetryAsync(
                job, progress, _copyCancellation.Token, logLines.Enqueue);
        }
        catch (OperationCanceledException)
        {
            result = new RobocopyResult(-1, CopyJobStatus.Cancelled,
                T("Hatalı öğeleri yeniden deneme işlemi iptal edildi.", "Retrying failed items was cancelled."));
        }

        job.ExitCode = result.ExitCode;
        job.Status = result.Status;
        job.Summary = result.Summary;
        job.Failures = result.Failures?.ToList() ?? [];
        job.FailedItemCount = result.FailedItemCount;
        job.CompletedAt = DateTimeOffset.Now;
        try { job.LogPath = await _jobLogStore.SaveAsync(job, logLines); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            job.Summary += " Günlük kaydedilemedi: " + ex.Message;
        }
        await _historyStore.AddAsync(job);
        var queueItem = _queue.FirstOrDefault(item => item.Job.Id == job.Id);
        queueItem?.SetResult(result with { Summary = LocalizeSummary(result.Summary) });
        ShowResult(job);
        await SaveQueueStateAsync();

        _isQueueRunning = false;
        SetRunningState(false);
        UpdateQueueState();
        _copyCancellation.Dispose();
        _copyCancellation = null;
        if (_settings.NotificationsEnabled)
            _notificationService.ShowTransferSummary(result.IsSuccessful ? 1 : 0,
                result.Status == CopyJobStatus.CompletedWithErrors ? 1 : 0,
                result.Status == CopyJobStatus.Failed ? 1 : 0);
    }

    private sealed record OptionChoice<T>(T Value, string Label);
    private sealed record HistoryChoice(CopyJob Job, string Label);
    private sealed record SavedLocationChoice(string Path, string Label);
    private sealed record ScheduleChoice(ScheduledTransfer Schedule, string Label);
}
