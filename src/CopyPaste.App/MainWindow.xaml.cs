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
    private readonly HistoryStore _historyStore = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly JobLogStore _jobLogStore = new();
    private readonly GitHubUpdateService _updateService = new();
    private readonly AppNotificationService _notificationService;
    private readonly ExplorerIntegrationService _explorerIntegration = new();
    private readonly TrayIconService _trayIcon;
    private readonly AppWindow _appWindow;
    private readonly ObservableCollection<QueueItemViewModel> _queue = [];
    private CancellationTokenSource? _copyCancellation;
    private QueueItemViewModel? _activeQueueItem;
    private bool _isQueueRunning;
    private bool _pauseRequested;
    private AppSettings _settings = new();
    private Uri? _availableUpdateUri;
    private bool _isUpdateCheckRunning;

    public MainWindow(ShellLaunchRequest? shellRequest = null, AppNotificationService? notificationService = null)
    {
        InitializeComponent();
        _notificationService = notificationService ?? new AppNotificationService();
        _trayIcon = new TrayIconService(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        ProfileComboBox.ItemsSource = CopyProfiles.All;
        ProfileComboBox.SelectedIndex = 0;
        ExistingFileBehaviorComboBox.ItemsSource = new[]
        {
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Update, "Yalnızca gerekenleri güncelle"),
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Skip, "Hedefte bulunanları atla"),
            new OptionChoice<ExistingFileBehavior>(ExistingFileBehavior.Overwrite, "Tüm dosyaların üzerine yaz")
        };
        ExistingFileBehaviorComboBox.SelectedIndex = 0;
        VerificationModeComboBox.ItemsSource = new[]
        {
            new OptionChoice<VerificationMode>(VerificationMode.Size, "Hızlı — dosya boyutu"),
            new OptionChoice<VerificationMode>(VerificationMode.Sha256, "Tam — SHA-256"),
            new OptionChoice<VerificationMode>(VerificationMode.None, "Doğrulama yapma")
        };
        VerificationModeComboBox.SelectedIndex = 0;
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
        await LoadHistoryAsync();
        if (autoStart && await AddCurrentTransferToQueueAsync())
            await RunQueueAsync();
        await CheckForUpdatesAsync(manual: false);
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(manual: true);

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_isUpdateCheckRunning)
            return;

        _isUpdateCheckRunning = true;
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Kontrol ediliyor…";
        try
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 1, 0, 0);
            var result = await _updateService.CheckAsync(currentVersion);
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    _availableUpdateUri = result.DownloadUri ?? result.ReleasePageUri;
                    UpdateStatusText.Text = $"Yeni sürüm hazır: {result.TagName}. " +
                                            $"Yüklü sürüm: {result.CurrentVersion.ToString(3)}.";
                    OpenUpdateButton.Content = "İndir";
                    OpenUpdateButton.Visibility = _availableUpdateUri is null
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    UpdateInfoBar.Visibility = Visibility.Visible;
                    if (_settings.NotificationsEnabled)
                        _notificationService.ShowUpdateAvailable(result.TagName ?? result.LatestVersion?.ToString(3) ?? "Yeni");
                    break;
                case UpdateCheckStatus.UpToDate when manual:
                    _availableUpdateUri = ProductInfo.LatestReleaseUri;
                    UpdateStatusText.Text = $"CopyPaste güncel — {result.CurrentVersion.ToString(3)}.";
                    OpenUpdateButton.Content = "Sürümleri aç";
                    OpenUpdateButton.Visibility = Visibility.Visible;
                    UpdateInfoBar.Visibility = Visibility.Visible;
                    break;
                case UpdateCheckStatus.RepositoryUnavailable when manual:
                    ShowUpdateCheckMessage("GitHub deposu veya yayınlanmış bir sürüm henüz bulunamadı.");
                    break;
                case UpdateCheckStatus.NetworkError or UpdateCheckStatus.InvalidResponse when manual:
                    ShowUpdateCheckMessage("Güncellemeler kontrol edilemedi. İnternet bağlantısını deneyip tekrar kontrol edin.");
                    break;
            }
        }
        finally
        {
            _isUpdateCheckRunning = false;
            CheckUpdatesButton.IsEnabled = true;
            CheckUpdatesButton.Content = "Güncellemeleri kontrol et";
        }
    }

    private void ShowUpdateCheckMessage(string message)
    {
        _availableUpdateUri = null;
        UpdateStatusText.Text = message;
        OpenUpdateButton.Visibility = Visibility.Collapsed;
        UpdateInfoBar.Visibility = Visibility.Visible;
    }

    private void OpenUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdateUri is not null)
            Process.Start(new ProcessStartInfo(_availableUpdateUri.AbsoluteUri) { UseShellExecute = true });
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
            SourceTextBox.Text = folder.Path;
        else
            DestinationTextBox.Text = folder.Path;
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is CopyProfile profile)
            ProfileDescriptionText.Text = $"{profile.Description} • {profile.ThreadCount} paralel iş parçacığı";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await AddCurrentTransferToQueueAsync();
    }

    private async Task<bool> AddCurrentTransferToQueueAsync()
    {
        ValidationInfoBar.Visibility = Visibility.Collapsed;
        PreflightInfoBar.Visibility = Visibility.Collapsed;
        var validation = CopyJobValidator.Validate(SourceTextBox.Text, DestinationTextBox.Text);
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
            SourcePath = Path.GetFullPath(SourceTextBox.Text.Trim()),
            DestinationPath = Path.GetFullPath(DestinationTextBox.Text.Trim()),
            Profile = profile,
            Options = optionsResult.Options!
        };

        _settings = GetSettingsFromUi();
        await _settingsStore.SaveAsync(_settings);

        StartButton.IsEnabled = false;
        StartButton.Content = "Analiz ediliyor…";
        try
        {
            var preflight = await _preflightAnalyzer.AnalyzeAsync(job.SourcePath, job.DestinationPath, job.Options);
            if (!preflight.HasEnoughSpace)
            {
                ValidationText.Text = "Hedef sürücüde bu transfer için yeterli boş alan yok.";
                ValidationInfoBar.Visibility = Visibility.Visible;
                return false;
            }

            _queue.Add(new QueueItemViewModel(job, preflight));
            var warningText = preflight.Warnings.Count == 0
                ? "Ön analiz tamamlandı."
                : string.Join(" ", preflight.Warnings);
            PreflightText.Text = $"{preflight.FileCount:N0} dosya • {preflight.DirectoryCount:N0} klasör • " +
                                 $"{QueueItemViewModel.FormatBytes(preflight.TotalBytes)}. {warningText}";
            PreflightInfoBar.Visibility = Visibility.Visible;
            UpdateQueueState();
            return true;
        }
        catch (OperationCanceledException)
        {
            ValidationText.Text = "Ön analiz iptal edildi.";
            ValidationInfoBar.Visibility = Visibility.Visible;
            return false;
        }
        finally
        {
            StartButton.Content = "Kuyruğa ekle";
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

        _isQueueRunning = true;
        _pauseRequested = false;
        SetRunningState(true);
        _copyCancellation = new CancellationTokenSource();

        foreach (var item in pendingItems)
        {
            _activeQueueItem = item;
            StatusText.Text = $"Kopyalanıyor: {item.Title}";
            CurrentFileText.Text = item.Paths;
            item.SetRunning();
            var progress = new Progress<RobocopyProgress>(UpdateProgress);
            var logLines = new ConcurrentQueue<string>();
            var result = await _runner.RunAsync(
                item.Job,
                progress,
                _copyCancellation.Token,
                logLines.Enqueue);
            if (_pauseRequested && result.Status == CopyJobStatus.Cancelled)
            {
                result = new RobocopyResult(-1, CopyJobStatus.Paused,
                    "Transfer duraklatıldı; devam komutunda kaldığı yerden sürdürülecek.");
            }
            if (result.Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings
                && item.Job.Options.Verification != VerificationMode.None)
            {
                try
                {
                    StatusText.Text = $"Doğrulanıyor: {item.Title}";
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
                            "Doğrulama duraklatıldı; transfer yeniden doğrulanabilir.")
                        : new RobocopyResult(-1, CopyJobStatus.Cancelled,
                            "Doğrulama kullanıcı tarafından iptal edildi.");
                }
            }

            item.Job.ExitCode = result.ExitCode;
            item.Job.Status = result.Status;
            item.Job.Summary = result.Summary;
            item.Job.CompletedAt = DateTimeOffset.Now;
            if (result.Status == CopyJobStatus.Paused)
                item.SetPaused();
            else
                item.SetResult(result);
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
        var failedCount = pendingItems.Count(item => item.Job.Status == CopyJobStatus.Failed);
        if (!_pauseRequested && _settings.NotificationsEnabled)
            _notificationService.ShowTransferSummary(completedCount, failedCount);
        _copyCancellation?.Dispose();
        _copyCancellation = null;
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
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _pauseRequested = false;
        StatusText.Text = "İptal ediliyor…";
        CancelButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        _copyCancellation?.Cancel();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isQueueRunning)
            return;
        _pauseRequested = true;
        StatusText.Text = "Duraklatılıyor…";
        PauseButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        _copyCancellation?.Cancel();
    }

    private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning)
            return;
        _queue.Clear();
        UpdateQueueState();
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
    }

    private void RemoveQueueItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListView.SelectedItem is not QueueItemViewModel item)
            return;
        _queue.Remove(item);
        UpdateQueueState();
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListView.SelectedItem is not QueueItemViewModel item)
            return;
        if (item.Job.Status is CopyJobStatus.Failed or CopyJobStatus.Cancelled or CopyJobStatus.Paused)
            item.ResetForRetry();
        UpdateQueueState();
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Kaynak klasör olarak seç";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private void TrayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_trayIcon.HideToTray())
        {
            ValidationText.Text = "Sistem tepsisi simgesi oluşturulamadı.";
            ValidationInfoBar.Visibility = Visibility.Visible;
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
            ValidationText.Text = "Explorer entegrasyonu güncellenemedi: " + ex.Message;
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_notificationService.ShowTestNotification())
        {
            ValidationText.Text = "Windows bildirimi gösterilemedi.";
            ValidationInfoBar.Visibility = Visibility.Visible;
        }
    }

    public void RestoreFromTray() => _trayIcon.Restore();

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isQueueRunning)
            return;

        if (_settings.MinimizeToTrayWhileRunning)
        {
            args.Cancel = true;
            _trayIcon.HideToTray();
        }
        else
        {
            _pauseRequested = false;
            _copyCancellation?.Cancel();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _appWindow.Closing -= AppWindow_Closing;
        _trayIcon.Dispose();
    }

    private void UpdateIntegrationState()
    {
        var explorerText = _explorerIntegration.IsRegistered
            ? "Kopyala/yapıştır menüleri etkin; Windows 11’de ‘Daha fazla seçenek göster’ altında görünür."
            : "Explorer kopyala/yapıştır menüleri henüz etkin değil.";
        var notificationText = _notificationService.IsAvailable
            ? "Windows bildirimleri hazır."
            : "Windows bildirimleri bu oturumda kullanılamıyor.";
        IntegrationStatusText.Text = explorerText + " " + notificationText;
        ExplorerIntegrationButton.Content = _explorerIntegration.IsRegistered
            ? "Sağ tık menüsünü kaldır"
            : "Sağ tık menüsünü ekle";
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
            DropHintText.Text = $"Kaynak seçildi: {folder.Name}";
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
            Content = "Geçmişi temizle",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = choices.Count > 0
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(choices.Count == 0
            ? new TextBlock { Text = "Henüz kayıtlı transfer bulunmuyor." }
            : list);
        panel.Children.Add(clearButton);

        var dialog = new ContentDialog
        {
            Title = "Transfer geçmişi",
            Content = panel,
            PrimaryButtonText = "Forma yükle",
            SecondaryButtonText = "Günlüğü aç",
            CloseButtonText = "Kapat",
            XamlRoot = Content.XamlRoot
        };
        clearButton.Click += async (_, _) =>
        {
            await _historyStore.ClearAsync();
            choices.Clear();
            list.ItemsSource = null;
            clearButton.IsEnabled = false;
            LastJobTitle.Text = "Henüz tamamlanan işlem yok";
            LastJobDetails.Text = "İlk transferiniz burada görünecek.";
        };
        var result = await dialog.ShowAsync();
        if (list.SelectedItem is not HistoryChoice selected)
            return;
        if (result == ContentDialogResult.Primary)
            LoadJobIntoForm(selected.Job);
        else if (result == ContentDialogResult.Secondary)
            OpenLog(selected.Job.LogPath);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ContinueOnErrorToggle.StartBringIntoView();
        ContinueOnErrorToggle.Focus(FocusState.Programmatic);
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = GetSettingsFromUi();
        await _settingsStore.SaveAsync(_settings);
        PreflightText.Text = "Çalışma ayarları kaydedildi.";
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        ApplySettingsToUi(_settings);
    }

    private AppSettings GetSettingsFromUi() => new()
    {
        DefaultProfileId = (ProfileComboBox.SelectedItem as CopyProfile)?.Id ?? "balanced",
        ExistingFiles = (ExistingFileBehaviorComboBox.SelectedItem as OptionChoice<ExistingFileBehavior>)?.Value
            ?? ExistingFileBehavior.Update,
        Verification = (VerificationModeComboBox.SelectedItem as OptionChoice<VerificationMode>)?.Value
            ?? VerificationMode.Size,
        FilePatterns = FilePatternsTextBox.Text,
        ExcludedDirectories = ExcludedDirectoriesTextBox.Text,
        ContinueQueueOnError = ContinueOnErrorToggle.IsOn,
        NotificationsEnabled = NotificationsToggle.IsOn,
        MinimizeToTrayWhileRunning = MinimizeOnCloseToggle.IsOn
    };

    private void ApplySettingsToUi(AppSettings settings)
    {
        ProfileComboBox.SelectedItem = CopyProfiles.All.FirstOrDefault(profile => profile.Id == settings.DefaultProfileId)
            ?? CopyProfiles.All[0];
        SelectChoice(ExistingFileBehaviorComboBox, settings.ExistingFiles);
        SelectChoice(VerificationModeComboBox, settings.Verification);
        FilePatternsTextBox.Text = string.IsNullOrWhiteSpace(settings.FilePatterns) ? "*" : settings.FilePatterns;
        ExcludedDirectoriesTextBox.Text = settings.ExcludedDirectories;
        ContinueOnErrorToggle.IsOn = settings.ContinueQueueOnError;
        NotificationsToggle.IsOn = settings.NotificationsEnabled;
        MinimizeOnCloseToggle.IsOn = settings.MinimizeToTrayWhileRunning;
    }

    private static void SelectChoice<T>(ComboBox comboBox, T value) where T : struct, Enum
    {
        comboBox.SelectedItem = comboBox.Items.Cast<object>()
            .OfType<OptionChoice<T>>()
            .FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, value));
    }

    private void LoadJobIntoForm(CopyJob job)
    {
        SourceTextBox.Text = job.SourcePath;
        DestinationTextBox.Text = job.DestinationPath;
        ProfileComboBox.SelectedItem = CopyProfiles.All.FirstOrDefault(profile => profile.Id == job.Profile.Id)
            ?? CopyProfiles.All[0];
        SelectChoice(ExistingFileBehaviorComboBox, job.Options.ExistingFiles);
        SelectChoice(VerificationModeComboBox, job.Options.Verification);
        FilePatternsTextBox.Text = string.Join(';', job.Options.FilePatterns);
        ExcludedDirectoriesTextBox.Text = string.Join(';', job.Options.ExcludedDirectories);
        PreflightText.Text = "Geçmişteki transfer forma yüklendi; kontrol edip kuyruğa ekleyebilirsiniz.";
        PreflightInfoBar.Visibility = Visibility.Visible;
    }

    private void OpenLog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ValidationText.Text = "Bu transfer için günlük dosyası bulunamadı.";
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
        FilePatternsTextBox.IsEnabled = !running;
        ExcludedDirectoriesTextBox.IsEnabled = !running;
        StartButton.IsEnabled = !running;
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
            StatusText.Text = "Kopyalanıyor";
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
        CopyProgressBar.Value = job.Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings ? 100 : 0;
        PercentageText.Text = job.Status is CopyJobStatus.Completed or CopyJobStatus.CompletedWithWarnings ? "100%" : "—";
        StatusText.Text = job.Status switch
        {
            CopyJobStatus.Completed => "Transfer tamamlandı",
            CopyJobStatus.CompletedWithWarnings => "Uyarılarla tamamlandı",
            CopyJobStatus.Cancelled => "Transfer iptal edildi",
            CopyJobStatus.Paused => "Transfer duraklatıldı",
            _ => "Transfer başarısız"
        };
        CurrentFileText.Text = job.Summary;
        LastJobTitle.Text = StatusText.Text;
        LastJobDetails.Text = $"{job.SourcePath} → {job.DestinationPath}\n{job.Summary}";
    }

    private async Task LoadHistoryAsync()
    {
        var jobs = await _historyStore.LoadAsync();
        if (jobs.FirstOrDefault() is not { } job)
            return;

        LastJobTitle.Text = GetStatusLabel(job.Status);
        LastJobDetails.Text = $"{job.SourcePath} → {job.DestinationPath}\n{job.Summary}";
    }

    private void UpdateQueueState()
    {
        QueueSummaryText.Text = $"{_queue.Count:N0} iş";
        EmptyQueueText.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var hasRunnableItem = _queue.Any(item => item.Job.Status is CopyJobStatus.Ready or CopyJobStatus.Paused);
        StartQueueButton.IsEnabled = !_isQueueRunning && hasRunnableItem;
        StartQueueButton.Content = _queue.Any(item => item.Job.Status == CopyJobStatus.Paused)
            ? "Kuyruğa devam et"
            : "Kuyruğu başlat";
        ClearQueueButton.IsEnabled = !_isQueueRunning && _queue.Count > 0;
        var selected = QueueListView.SelectedItem as QueueItemViewModel;
        var selectedIndex = selected is null ? -1 : _queue.IndexOf(selected);
        MoveUpButton.IsEnabled = !_isQueueRunning && selectedIndex > 0;
        MoveDownButton.IsEnabled = !_isQueueRunning && selectedIndex >= 0 && selectedIndex < _queue.Count - 1;
        RemoveQueueItemButton.IsEnabled = !_isQueueRunning && selected is not null;
        RetryButton.IsEnabled = !_isQueueRunning
            && selected?.Job.Status is CopyJobStatus.Failed or CopyJobStatus.Cancelled or CopyJobStatus.Paused;
    }

    private static string GetStatusLabel(CopyJobStatus status) => status switch
    {
        CopyJobStatus.Ready => "Hazır",
        CopyJobStatus.Running => "Kopyalanıyor",
        CopyJobStatus.Paused => "Duraklatıldı",
        CopyJobStatus.Completed => "Tamamlandı",
        CopyJobStatus.CompletedWithWarnings => "Uyarılarla tamamlandı",
        CopyJobStatus.Cancelled => "İptal edildi",
        _ => "Başarısız"
    };

    private sealed record OptionChoice<T>(T Value, string Label);
    private sealed record HistoryChoice(CopyJob Job, string Label);
}
