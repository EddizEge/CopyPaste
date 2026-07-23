using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using System.Net;
using System.Text;
using System.Security.Cryptography;

var failures = new List<string>();

Assert(RobocopyRunner.CreateResult(0).Status == CopyJobStatus.Completed, "Exit 0 başarılı olmalı");
Assert(RobocopyRunner.CreateResult(1).Status == CopyJobStatus.Completed, "Exit 1 başarılı olmalı");
Assert(RobocopyRunner.CreateResult(3).Status == CopyJobStatus.CompletedWithWarnings, "Exit 3 uyarılı olmalı");
Assert(RobocopyRunner.CreateResult(8).Status == CopyJobStatus.CompletedWithErrors,
    "Exit 8 hatalarla tamamlandı sayılmalı");
Assert(RobocopyRunner.CreateResult(16).Status == CopyJobStatus.Failed,
    "Exit 16 ciddi hata olarak başarısız olmalı");
Assert(!new RobocopyResult(-1, CopyJobStatus.Cancelled, "iptal").IsSuccessful,
    "İptal sonucu başarılı sayılmamalı");
Assert(ReleaseNotesCatalog.All is [{ Version: "1.6.0", IsPreview: false }, ..]
       && ReleaseNotesCatalog.All.Select(note => note.Version).Distinct(StringComparer.OrdinalIgnoreCase).Count()
          == ReleaseNotesCatalog.All.Count
       && ReleaseNotesCatalog.All.All(note => note.TurkishChanges.Count > 0
           && note.TurkishChanges.Count == note.EnglishChanges.Count
           && note.TurkishChanges.All(change => !string.IsNullOrWhiteSpace(change))
           && note.EnglishChanges.All(change => !string.IsNullOrWhiteSpace(change))),
    "Sürüm notları yeni sürümden eskiye sıralı, benzersiz ve Türkçe/İngilizce eksiksiz olmalı");
var protectedSelection = ProtectedFolderSelectionSerializer.Parse(
    ProtectedFolderSelectionSerializer.Serialize([@"C:\Korumalı\Bir", @"C:\Korumalı\İki", @"C:\Korumalı\Bir"]));
Assert(protectedSelection.SequenceEqual([@"C:\Korumalı\Bir", @"C:\Korumalı\İki"])
       && ProtectedFolderSelectionSerializer.Parse(@"C:\Eski-Tek-Seçim").Count == 1,
    "Korumalı çoklu klasör seçimi benzersiz JSON yollarını ve eski tek-yol biçimini desteklemeli");

var partialOutput = new[]
{
    @"2026/07/17 09:15:32 ERROR 5 (0x00000005) Copying File C:\Arşiv\kilitli-rapor.xlsx",
    "Access is denied.",
    @"2026/07/17 09:15:33 ERROR 32 (0x00000020) Copying File C:\Arşiv\kullanımda.pst",
    "The process cannot access the file because it is being used by another process.",
    "   Files :     86448     86443         3         0         2         0"
};
var partialAnalysis = RobocopyOutputAnalyzer.Analyze(partialOutput);
var partialResult = RobocopyRunner.CreateResult(8, partialAnalysis);
Assert(partialAnalysis.CopiedFileCount == 86443
       && partialAnalysis.FailedItemCount == 2
       && partialAnalysis.Failures.Count == 2,
    "Robocopy özeti ve kopyalanamayan dosyalar ayrıştırılmalı");
Assert(partialResult.Status == CopyJobStatus.CompletedWithErrors
       && partialResult.Summary.Contains("2 öğe"),
    "Kısmi hata özeti kullanıcıya doğru sayıyı göstermeli");
Assert(GitHubUpdateService.TryParseVersion("v1.2.3-beta.1", out var parsedVersion)
       && parsedVersion == new Version(1, 2, 3, 0),
    "GitHub sürüm etiketleri güvenli biçimde ayrıştırılmalı");

var releaseJson = """
{
  "tag_name": "v1.2.0",
  "html_url": "https://github.com/EddizEge/CopyPaste/releases/tag/v1.2.0",
  "body": "Yeni özellikler",
  "assets": [
    {
      "name": "CopyPaste-1.2.0-Setup.exe",
      "browser_download_url": "https://github.com/EddizEge/CopyPaste/releases/download/v1.2.0/CopyPaste-1.2.0-Setup.exe",
      "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    },
    {
      "name": "CopyPaste-1.2.0-win-x64.zip",
      "browser_download_url": "https://github.com/EddizEge/CopyPaste/releases/download/v1.2.0/CopyPaste-1.2.0-win-x64.zip"
    }
  ]
}
""";
var updateClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, releaseJson));
var updateService = new GitHubUpdateService(updateClient);
var availableUpdate = await updateService.CheckAsync(new Version(1, 1, 0));
Assert(availableUpdate.HasUpdate
       && availableUpdate.LatestVersion == new Version(1, 2, 0, 0)
       && availableUpdate.DownloadUri?.AbsoluteUri.EndsWith("CopyPaste-1.2.0-Setup.exe") == true
       && availableUpdate.AssetName == "CopyPaste-1.2.0-Setup.exe"
       && availableUpdate.Sha256Digest?.StartsWith("sha256:") == true,
    "Yeni GitHub Release içinde kurulum dosyası ZIP paketine tercih edilmeli");
var currentUpdate = await updateService.CheckAsync(new Version(1, 2, 0));
Assert(currentUpdate.Status == UpdateCheckStatus.UpToDate,
    "Aynı sürüm güncel kabul edilmeli");
var missingUpdateService = new GitHubUpdateService(
    new HttpClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, "{}")));
Assert((await missingUpdateService.CheckAsync(new Version(1, 1, 0))).Status
       == UpdateCheckStatus.RepositoryUnavailable,
    "Henüz yayınlanmamış GitHub deposu anlaşılır sonuç vermeli");
var malformedUpdateService = new GitHubUpdateService(
    new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{\"tag_name\":[]}")));
Assert((await malformedUpdateService.CheckAsync(new Version(1, 1, 0))).Status
       == UpdateCheckStatus.InvalidResponse,
    "Bozuk GitHub yanıtı uygulamayı kapatmadan reddedilmeli");

var tempSource = Path.Combine(Path.GetTempPath(), "CopyPasteTests", Guid.NewGuid().ToString("N"));
var tempDestination = tempSource + "-destination";
var shellStateDirectory = tempSource + "-shell-state";
var storeDirectory = tempSource + "-stores";
Directory.CreateDirectory(tempSource);

try
{
    Assert(CopyJobValidator.Validate(tempSource, tempSource).IsValid == false, "Aynı yollar reddedilmeli");
    Assert(CopyJobValidator.Validate(tempSource, Path.Combine(tempSource, "child")).IsValid == false, "İç içe yollar reddedilmeli");
    Assert(CopyJobValidator.Validate(tempSource, tempDestination).IsValid, "Bağımsız yollar kabul edilmeli");
    Assert(StartupPathResolver.Resolve(["CopyPaste.exe", $"\"{tempSource}\""]) == tempSource,
        "Explorer komut satırı klasörü kaynak olarak çözülmeli");
    var scheduleId = Guid.NewGuid();
    var scheduledRequest = ShellLaunchRequestResolver.Resolve(
        ["CopyPaste.exe", "--schedule", scheduleId.ToString("D")], new ShellCopyStateStore(shellStateDirectory));
    Assert(scheduledRequest.Mode == ShellLaunchMode.Scheduled
           && scheduledRequest.ScheduleId == scheduleId
           && scheduledRequest.AutoStart,
        "Windows Görev Zamanlayıcı isteği güvenli biçimde ayrıştırılmalı");
    var shellState = new ShellCopyStateStore(shellStateDirectory);
    var copyRequest = ShellLaunchRequestResolver.Resolve(["CopyPaste.exe", "--copy", tempSource], shellState);
    Assert(copyRequest.Mode == ShellLaunchMode.Copy && copyRequest.SourcePath == tempSource,
        "Explorer kopyala komutu kaynağı hatırlamalı");
    var pasteRequest = ShellLaunchRequestResolver.Resolve(["CopyPaste.exe", "--paste", tempDestination], shellState);
    Assert(pasteRequest.Mode == ShellLaunchMode.Paste
           && pasteRequest.SourcePath == tempSource
           && pasteRequest.DestinationPath == tempDestination
           && pasteRequest.AutoStart,
        "Explorer yapıştır komutu kaynak ve hedefi otomatik başlatmalı");
    var secondSource = Path.Combine(tempSource, "ikinci-kaynak");
    Directory.CreateDirectory(secondSource);
    var multiCopyRequest = ShellLaunchRequestResolver.Resolve(
        ["CopyPaste.exe", "--copy", tempSource, secondSource], shellState);
    var multiPasteRequest = ShellLaunchRequestResolver.Resolve(
        ["CopyPaste.exe", "--paste", tempDestination], shellState);
    Assert(multiCopyRequest.SourcePaths?.Count == 2
           && multiPasteRequest.SourcePaths?.Count == 2
           && multiPasteRequest.AutoStart,
        "Explorer birden fazla seçili klasörü tek kuyruğa aktarabilmeli");

    var profile = CopyProfiles.All[0];
    var job = new CopyJob { SourcePath = tempSource, DestinationPath = tempDestination, Profile = profile };
    var info = RobocopyCommandBuilder.Build(job);
    Assert(info.FileName.EndsWith("robocopy.exe", StringComparison.OrdinalIgnoreCase), "Robocopy çalıştırılmalı");
    Assert(info.ArgumentList.Contains("/MT:16"), "Profil thread sayısı uygulanmalı");
    Assert(!info.ArgumentList.Contains("/MIR"), "Tehlikeli aynalama varsayılan olmamalı");
    var previewInfo = RobocopyCommandBuilder.Build(job, listOnly: true);
    Assert(previewInfo.ArgumentList.Contains("/L")
           && previewInfo.ArgumentList.Contains("/V")
           && previewInfo.ArgumentList.Contains("/NP")
           && !previewInfo.ArgumentList.Contains("/MIR")
           && !previewInfo.ArgumentList.Contains("/PURGE"),
        "Önizleme gerçek ve güvenli Robocopy listeleme anahtarlarını kullanmalı");
    var parsedPlan = RobocopyPlanService.Analyze(job,
    [
        @"              same               7    C:\Kaynak\aynı.txt",
        @"         Older                  26    C:\Kaynak\değişti.txt",
        @"         New File                6    C:\Kaynak\yeni.txt",
        "   Files :         3         2         1         0         0         0",
        "   Bytes :        39        32         7         0         0         0"
    ], exitCode: 1);
    Assert(parsedPlan.TotalFileCount == 3
           && parsedPlan.CopyFileCount == 1
           && parsedPlan.OverwriteFileCount == 1
           && parsedPlan.SkippedFileCount == 1
           && parsedPlan.BytesToCopy == 32
           && parsedPlan.Items.Count == 3,
        "Robocopy /L çıktısı yeni, üzerine yazılacak ve atlanacak öğeleri ayrıştırmalı");
    var failedPlan = RobocopyPlanService.Analyze(job,
    [
        @"2026/07/17 09:15:32 ERROR 5 (0x00000005) Accessing Source Directory C:\Korumalı\",
        "   Files :         1         0         0         0         1         0",
        "   Bytes :         0         0         0         0         0         0"
    ], exitCode: 8);
    Assert(failedPlan.HasErrors
           && failedPlan.FailedFileCount == 1
           && failedPlan.Items is [{ Action: CopyPlanAction.Error }],
        "Robocopy /L erişim hatası yol ayrıntısıyla plana eklenmeli");
    Assert(CopyDestinationResolver.Resolve(@"C:\Kaynak\Belgeler", @"D:\Yedek", CopyRootMode.SelectedFolder)
               == @"D:\Yedek\Belgeler"
           && CopyDestinationResolver.Resolve(@"C:\Kaynak\Belgeler", @"D:\Yedek", CopyRootMode.ContentsOnly)
               == @"D:\Yedek",
        "Seçilen klasör ve yalnız içerik hedefleri doğru çözümlenmeli");
    var lowResourceJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = CopyProfiles.All[1],
        RequestedPerformanceMode = TransferPerformanceMode.LowResource,
        ActivePerformanceMode = TransferPerformanceMode.LowResource,
        BandwidthLimitMbps = 25,
        UseBackupMode = true
    };
    var lowResourceInfo = RobocopyCommandBuilder.Build(lowResourceJob);
    Assert(lowResourceInfo.ArgumentList.Contains("/MT:4")
           && lowResourceInfo.ArgumentList.Contains("/IPG:25")
           && lowResourceInfo.ArgumentList.Contains("/IORATE:25m")
           && lowResourceInfo.ArgumentList.Contains("/ZB"),
        "Düşük kaynak modu iş parçacığını ve disk erişim temposunu sınırlamalı");
    var policyJobs = new[]
    {
        new CopyJob
        {
            SourcePath = tempSource,
            DestinationPath = tempDestination,
            Profile = profile,
            Status = CopyJobStatus.Completed,
            CompletionAction = CompletionAction.Sleep
        },
        new CopyJob
        {
            SourcePath = tempSource,
            DestinationPath = tempDestination,
            Profile = profile,
            Status = CopyJobStatus.CompletedWithWarnings,
            CompletionAction = CompletionAction.ShutDown
        }
    };
    Assert(CompletionActionPolicy.ResolveForCompletedRun(policyJobs) == CompletionAction.ShutDown,
        "Tümü başarılı kuyrukta yalnızca son işin tamamlanma eylemi seçilmeli");
    policyJobs[0].Status = CopyJobStatus.CompletedWithErrors;
    Assert(CompletionActionPolicy.ResolveForCompletedRun(policyJobs) == CompletionAction.None,
        "Kısmi hata bulunan kuyrukta hiçbir güç eylemi çalışmamalı");

    var settingsStore = new SettingsStore(storeDirectory);
    var savedSettings = new AppSettings
    {
        DefaultProfileId = "large",
        ExistingFiles = ExistingFileBehavior.Overwrite,
        Verification = VerificationMode.Sha256,
        FilePatterns = "*.bin",
        ExcludedDirectories = ".git",
        ContinueQueueOnError = false,
        NotificationsEnabled = false,
        MinimizeToTrayWhileRunning = false,
        AutoDownloadUpdates = false,
        PerformanceMode = TransferPerformanceMode.LowResource,
        CopyRootMode = CopyRootMode.SelectedFolder,
        BandwidthLimitMbps = 40,
        CompletionAction = CompletionAction.Sleep,
        StartWithWindows = true,
        StartMinimizedWithWindows = true,
        Language = "en-US",
        FavoriteLocations = [new("Test", tempSource)],
        RecentSources = [tempSource],
        RecentDestinations = [tempDestination]
    };
    await settingsStore.SaveAsync(savedSettings);
    var loadedSettings = await settingsStore.LoadAsync();
    Assert(loadedSettings.DefaultProfileId == savedSettings.DefaultProfileId
           && loadedSettings.ExistingFiles == savedSettings.ExistingFiles
           && loadedSettings.Verification == savedSettings.Verification
           && loadedSettings.FilePatterns == savedSettings.FilePatterns
           && loadedSettings.ExcludedDirectories == savedSettings.ExcludedDirectories
           && loadedSettings.ContinueQueueOnError == savedSettings.ContinueQueueOnError
           && loadedSettings.NotificationsEnabled == savedSettings.NotificationsEnabled
           && loadedSettings.MinimizeToTrayWhileRunning == savedSettings.MinimizeToTrayWhileRunning
           && loadedSettings.AutoDownloadUpdates == savedSettings.AutoDownloadUpdates
           && loadedSettings.PerformanceMode == TransferPerformanceMode.LowResource
           && loadedSettings.CopyRootMode == CopyRootMode.SelectedFolder
           && loadedSettings.BandwidthLimitMbps == 40
           && loadedSettings.CompletionAction == CompletionAction.Sleep
           && loadedSettings.StartMinimizedWithWindows
           && loadedSettings.Language == "en-US"
           && loadedSettings.FavoriteLocations.SequenceEqual(savedSettings.FavoriteLocations)
           && loadedSettings.RecentSources.SequenceEqual(savedSettings.RecentSources),
        "Uygulama ayarları kalıcı olmalı");

    var updateBytes = Encoding.UTF8.GetBytes("CopyPaste güvenli güncelleme paketi");
    var updateDigest = Convert.ToHexString(SHA256.HashData(updateBytes)).ToLowerInvariant();
    var updateDownload = new UpdateDownloadService(new HttpClient(new ByteStubHttpMessageHandler(updateBytes)));
    var updateDownloadResult = await updateDownload.DownloadAsync(
        new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            new Version(1, 2, 0),
            new Version(1, 3, 0),
            DownloadUri: new Uri("https://github.com/EddizEge/CopyPaste/releases/download/v1.3.0/CopyPaste-1.3.0-Setup.exe"),
            AssetName: "CopyPaste-1.3.0-Setup.exe",
            Sha256Digest: "sha256:" + updateDigest),
        Path.Combine(storeDirectory, "updates"));
    Assert(updateDownloadResult.Success && File.Exists(updateDownloadResult.FilePath),
        $"Güncelleme doğru SHA-256 ile güvenli biçimde indirilmeli ({updateDownloadResult.Error ?? updateDownloadResult.FilePath})");
    var rejectedUpdate = await updateDownload.DownloadAsync(
        new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            new Version(1, 2, 0),
            new Version(1, 3, 0),
            DownloadUri: new Uri("https://github.com/EddizEge/CopyPaste/releases/download/v1.3.0/CopyPaste-1.3.0-Setup.exe"),
            AssetName: "CopyPaste-1.3.0-Setup.exe",
            Sha256Digest: "sha256:" + new string('0', 64)),
        Path.Combine(storeDirectory, "updates"));
    Assert(!rejectedUpdate.Success && rejectedUpdate.Error?.Contains("SHA-256") == true,
        "Hash değeri uyuşmayan güncelleme reddedilmeli");

    var recoverableJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = CopyProfiles.All[0],
        BandwidthLimitMbps = 64,
        CompletionAction = CompletionAction.Sleep,
        Status = CopyJobStatus.Running
    };
    var queueStateStore = new QueueStateStore(storeDirectory);
    await queueStateStore.SaveAsync([recoverableJob]);
    var recoveredQueue = await queueStateStore.LoadAsync();
    Assert(recoveredQueue.Count == 1
           && recoveredQueue[0].Status == CopyJobStatus.Paused
           && recoveredQueue[0].BandwidthLimitMbps == 64
           && recoveredQueue[0].CompletionAction == CompletionAction.Sleep
           && recoveredQueue[0].Summary?.Contains("kurtarıldı") == true,
        "Çökme sırasında çalışan kuyruk işi duraklatılmış olarak kurtarılmalı");

    var scheduleStore = new ScheduleStore(storeDirectory);
    var scheduledTransfer = new ScheduledTransfer
    {
        Name = "Gece yedeği",
        TimeOfDay = "02:30",
        Kind = ScheduleKind.Weekly,
        DayOfWeek = DayOfWeek.Saturday,
        Job = new CopyJob
        {
            SourcePath = tempSource,
            DestinationPath = tempDestination,
            Profile = CopyProfiles.All[0],
            BandwidthLimitMbps = 32,
            CompletionAction = CompletionAction.ShutDown
        }
    };
    await scheduleStore.SaveAsync(scheduledTransfer);
    Assert((await scheduleStore.FindAsync(scheduledTransfer.Id)) is
        {
            TimeOfDay: "02:30",
            Kind: ScheduleKind.Weekly,
            DayOfWeek: DayOfWeek.Saturday,
            Job.BandwidthLimitMbps: 32,
            Job.CompletionAction: CompletionAction.ShutDown
        },
        "Zamanlanmış transfer tüm iş seçenekleriyle saklanmalı");
    await scheduleStore.RemoveAsync(scheduledTransfer.Id);
    Assert(await scheduleStore.FindAsync(scheduledTransfer.Id) is null,
        "Zamanlanmış transfer kaldırılabilmeli");

    Assert(NetworkAvailabilityService.IsNetworkPath(@"\\sunucu\arsiv")
           && !NetworkAvailabilityService.IsNetworkPath(tempSource),
        "UNC ağ yolları yerel klasörlerden ayırt edilmeli");
    Assert(await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        tempSource, tempDestination, TimeSpan.FromMilliseconds(10)),
        "Yerel transferler ağ beklemesine girmemeli");

    var parsedOptions = CopyJobOptionsParser.Parse(
        ExistingFileBehavior.Skip,
        VerificationMode.Sha256,
        "*.txt;*.bin;*.txt",
        "node_modules;.git");
    Assert(parsedOptions.IsValid, "Güvenli gelişmiş seçenekler kabul edilmeli");
    Assert(parsedOptions.Options?.FilePatterns.Count == 2, "Tekrarlanan filtreler ayıklanmalı");
    Assert(!CopyJobOptionsParser.Parse(
            ExistingFileBehavior.Update,
            VerificationMode.Size,
            "/MIR",
            null).IsValid,
        "Robocopy anahtarı dosya filtresi olarak kabul edilmemeli");

    var advancedJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = profile,
        Options = parsedOptions.Options!
    };
    var advancedInfo = RobocopyCommandBuilder.Build(advancedJob);
    Assert(advancedInfo.ArgumentList.Contains("*.txt") && advancedInfo.ArgumentList.Contains("*.bin"),
        "Dosya filtreleri Robocopy komutuna eklenmeli");
    Assert(advancedInfo.ArgumentList.Contains("/XC")
           && advancedInfo.ArgumentList.Contains("/XN")
           && advancedInfo.ArgumentList.Contains("/XO"),
        "Mevcut dosyaları atlama anahtarları uygulanmalı");
    Assert(advancedInfo.ArgumentList.Contains("/XD")
           && advancedInfo.ArgumentList.Contains("node_modules")
           && advancedInfo.ArgumentList.Contains(".git"),
        "Hariç tutulan klasörler güvenli biçimde uygulanmalı");
    var missingFailure = new CopyFailure(Path.Combine(tempSource, "artık-yok.txt"), "Dosya kaldırıldı");
    var missingFailureJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = profile,
        Status = CopyJobStatus.CompletedWithErrors,
        FailedItemCount = 1,
        Failures = [missingFailure]
    };
    var missingRetryResult = await new FailedItemRetryService().RetryAsync(missingFailureJob);
    Assert(missingRetryResult.Status == CopyJobStatus.Failed
           && missingRetryResult.Failures?.SequenceEqual([missingFailure]) == true,
        "Artık bulunmayan seçili hata yolu yeniden denenemese bile özgün hata listesi korunmalı");

    var unicodeFolder = Path.Combine(tempSource, "Türkçe-文件", new string('d', 48));
    Directory.CreateDirectory(unicodeFolder);
    for (var index = 0; index < 100; index++)
        await File.WriteAllTextAsync(Path.Combine(unicodeFolder, $"küçük-dosya-{index:000}.txt"), $"CopyPaste test verisi {index}");

    var largeFile = new byte[8 * 1024 * 1024];
    new Random(42).NextBytes(largeFile);
    await File.WriteAllBytesAsync(Path.Combine(tempSource, "büyük-dosya.bin"), largeFile);

    var analyzer = new CopyPreflightAnalyzer();
    var initialAnalysis = await analyzer.AnalyzeAsync(tempSource, tempDestination);
    Assert(initialAnalysis.FileCount == 101, "Ön analiz tüm dosyaları saymalı");
    Assert(initialAnalysis.DirectoryCount >= 2, "Ön analiz alt klasörleri saymalı");
    Assert(initialAnalysis.TotalBytes > 8 * 1024 * 1024, "Ön analiz toplam boyutu hesaplamalı");
    Assert(initialAnalysis.ExistingFileCount == 0, "İlk analizde hedef çakışması olmamalı");
    Assert(initialAnalysis.HasEnoughSpace, "Yerel test hedefinde yeterli alan olmalı");
    job.EstimatedTotalBytes = initialAnalysis.TotalBytes;
    job.EstimatedFileCount = initialAnalysis.FileCount;

    var livePlan = await new RobocopyPlanService().PlanAsync(job);
    Assert(livePlan.CopyFileCount == 101
           && livePlan.OverwriteFileCount == 0
           && livePlan.SkippedFileCount == 0
           && livePlan.BytesToCopy == initialAnalysis.TotalBytes
           && livePlan.Items.Count == 101,
        $"Gerçek Robocopy /L önizlemesi transfer planını doğru üretmeli " +
        $"(yeni: {livePlan.CopyFileCount}, üzerine yaz: {livePlan.OverwriteFileCount}, " +
        $"atla: {livePlan.SkippedFileCount}, bayt: {livePlan.BytesToCopy})");

    var textOnlyOptions = new CopyJobOptions { FilePatterns = ["*.txt"] };
    var filteredAnalysis = await analyzer.AnalyzeAsync(tempSource, tempDestination, textOnlyOptions);
    Assert(filteredAnalysis.FileCount == 100, "Ön analiz dosya filtresini uygulamalı");
    Assert(filteredAnalysis.TotalBytes < initialAnalysis.TotalBytes, "Filtreli analiz yalnızca seçilen dosyaların boyutunu toplamalı");

    var runner = new RobocopyRunner();
    var liveProgress = new List<RobocopyProgress>();
    var result = await runner.RunAsync(job, new InlineProgress<RobocopyProgress>(liveProgress.Add));
    Assert(result.IsSuccessful, $"Gerçek Robocopy transferi başarılı olmalı (kod: {result.ExitCode})");
    Assert(liveProgress.Any(value => value.BytesTransferred > 0)
           && liveProgress.Any(value => value.BytesPerSecond > 0),
        $"Canlı Robocopy ilerlemesi aktarılan bayt ve hız bilgisi üretmeli: " +
        string.Join(" || ", liveProgress.Select(value => value.Message).Where(message => message.Contains("File")).Take(5)));

    var currentPlan = await new RobocopyPlanService().PlanAsync(job);
    Assert(currentPlan.CopyFileCount == 0
           && currentPlan.OverwriteFileCount == 0
           && currentPlan.SkippedFileCount == 101
           && currentPlan.BytesToCopy == 0,
        $"Güncel hedef için Robocopy /L planı tüm dosyaları atlamalı " +
        $"(yeni: {currentPlan.CopyFileCount}, üzerine yaz: {currentPlan.OverwriteFileCount}, " +
        $"atla: {currentPlan.SkippedFileCount}, bayt: {currentPlan.BytesToCopy})");
    var comparisonService = new CopyComparisonService();
    var filterEffectComparison = await comparisonService.CompareAsync(new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = profile,
        Options = textOnlyOptions
    });
    Assert(filterEffectComparison.CheckedFiles == 100
           && filterEffectComparison.ExcludedFiles == 1
           && filterEffectComparison.ExcludedBytes == largeFile.Length,
        "Önizleme dosya filtrelerinin dışarıda bıraktığı sayı ve boyutu göstermeli");

    var firstRelative = Path.GetRelativePath(tempSource,
        Directory.EnumerateFiles(tempSource, "*.txt", SearchOption.AllDirectories).First());
    var secondRelative = Path.GetRelativePath(tempSource,
        Directory.EnumerateFiles(tempSource, "*.txt", SearchOption.AllDirectories).Skip(1).First());
    File.Delete(Path.Combine(tempDestination, firstRelative));
    await File.WriteAllTextAsync(Path.Combine(tempDestination, secondRelative), "bozuk hedef içeriği");
    var damagedPlan = await new RobocopyPlanService().PlanAsync(job);
    Assert(damagedPlan.CopyFileCount == 1
           && damagedPlan.OverwriteFileCount == 1
           && damagedPlan.BytesToCopy > 0,
        $"Robocopy /L planı eksik ve değişmiş dosyaları ayırt etmeli " +
        $"(yeni: {damagedPlan.CopyFileCount}, üzerine yaz: {damagedPlan.OverwriteFileCount})");
    var damagedComparison = await comparisonService.CompareAsync(job);
    Assert(damagedComparison.MissingFiles == 1 && damagedComparison.SizeMismatches == 1,
        "Karşılaştırma eksik ve bozuk dosyaları ayırt etmeli");
    var repairJobs = CopyRepairService.CreateRepairJobs(job, damagedComparison.Differences);
    Assert(repairJobs.Count >= 1
           && repairJobs.Sum(value => value.Options.FilePatterns.Count) == 2
           && repairJobs.All(value => value.BandwidthLimitMbps == job.BandwidthLimitMbps
               && value.CompletionAction == job.CompletionAction),
        "Onarım yalnızca farklı dosyalar için güvenli işler üretmeli");
    foreach (var repairJob in repairJobs)
        Assert((await runner.RunAsync(repairJob)).IsSuccessful, "Onarım işi başarıyla tamamlanmalı");
    Assert(!(await comparisonService.CompareAsync(job)).NeedsRepair,
        "Onarım sonrasında kaynak ve hedef yeniden eşleşmeli");

    var partialSource = tempSource + "-partial";
    var partialDestination = tempDestination + "-partial";
    Directory.CreateDirectory(partialSource);
    try
    {
        await File.WriteAllTextAsync(Path.Combine(partialSource, "başarılı.txt"), "kopyalanabilir");
        var lockedPath = Path.Combine(partialSource, "kullanımda.txt");
        await File.WriteAllTextAsync(lockedPath, "kilitli");
        var partialJob = new CopyJob
        {
            SourcePath = partialSource,
            DestinationPath = partialDestination,
            Profile = new CopyProfile("partial-qa", "Kısmi hata testi", "", 1, false, 0, 0)
        };
        RobocopyResult livePartialResult;
        var partialLog = new List<string>();
        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            livePartialResult = await runner.RunAsync(partialJob, logLine: partialLog.Add);
            Assert(livePartialResult.Status == CopyJobStatus.CompletedWithErrors,
                $"Gerçek Robocopy kısmi hatası doğru sınıflanmalı (kod: {livePartialResult.ExitCode})");
            Assert(livePartialResult.FailedItemCount >= 1 && livePartialResult.Failures?.Count >= 1,
                $"Gerçek Robocopy çıktısından kilitli dosya ayrıntısı alınmalı: {string.Join(" || ", partialLog.Take(30))}");
        }
        partialJob.Status = livePartialResult.Status;
        partialJob.Failures = livePartialResult.Failures?.ToList() ?? [];
        partialJob.FailedItemCount = livePartialResult.FailedItemCount;
        var retryJobs = FailedItemRetryService.CreateRetryJobs(partialJob);
        Assert(retryJobs.Count == 1
               && retryJobs[0].Options.FilePatterns.SequenceEqual(["kullanımda.txt"]),
            $"Yalnızca kopyalanamayan dosya için güvenli yeniden deneme işi oluşturulmalı " +
            $"(hatalar: {string.Join(" | ", partialJob.Failures.Select(value => value.Path))}; işler: {retryJobs.Count})");
        var lockedFailure = partialJob.Failures.Single();
        var untouchedFailure = new CopyFailure(Path.Combine(partialSource, "başarılı.txt"), "Seçilmedi");
        partialJob.Failures.Add(untouchedFailure);
        partialJob.FailedItemCount = 2;
        var selectedRetryResult = await new FailedItemRetryService(runner).RetryAsync(
            partialJob, selectedFailures: [lockedFailure]);
        Assert(selectedRetryResult.Status == CopyJobStatus.CompletedWithErrors
               && selectedRetryResult.Failures?.SequenceEqual([untouchedFailure]) == true
               && File.Exists(Path.Combine(partialDestination, "kullanımda.txt")),
            "Seçili hata yeniden denenirken seçilmeyen hata sonuç listesinde korunmalı");
        partialJob.Failures = selectedRetryResult.Failures?.ToList() ?? [];
        partialJob.FailedItemCount = selectedRetryResult.FailedItemCount;
        var retryResult = await new FailedItemRetryService(runner).RetryAsync(partialJob);
        Assert(retryResult.IsSuccessful && File.Exists(Path.Combine(partialDestination, "kullanımda.txt")),
            $"Kilidi kaldırılan dosya yalnız başına yeniden denenip kopyalanmalı ({retryResult.Summary})");
    }
    finally
    {
        if (Directory.Exists(partialSource))
            Directory.Delete(partialSource, true);
        if (Directory.Exists(partialDestination))
            Directory.Delete(partialDestination, true);
    }

    foreach (var sourceFile in Directory.EnumerateFiles(tempSource, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(tempSource, sourceFile);
        var destinationFile = Path.Combine(tempDestination, relativePath);
        Assert(File.Exists(destinationFile), $"Hedefte dosya bulunmalı: {relativePath}");
        if (File.Exists(destinationFile))
            Assert(SHA256.HashData(await File.ReadAllBytesAsync(sourceFile)).SequenceEqual(
                SHA256.HashData(await File.ReadAllBytesAsync(destinationFile))), $"Hash eşleşmeli: {relativePath}");
    }

    var verificationJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = profile,
        Options = new CopyJobOptions { Verification = VerificationMode.Sha256 }
    };
    var verifier = new CopyVerificationService();
    var verification = await verifier.VerifyAsync(verificationJob);
    Assert(verification.IsSuccessful && verification.CheckedFiles == 101,
        "SHA-256 doğrulaması kopyalanan tüm dosyalarda başarılı olmalı");

    var corruptedDestination = Path.Combine(tempDestination, "büyük-dosya.bin");
    var corruptedBytes = await File.ReadAllBytesAsync(corruptedDestination);
    corruptedBytes[0] ^= 0xFF;
    await File.WriteAllBytesAsync(corruptedDestination, corruptedBytes);
    var failedVerification = await verifier.VerifyAsync(verificationJob);
    Assert(!failedVerification.IsSuccessful && failedVerification.HashMismatches == 1,
        "SHA-256 doğrulaması aynı boyuttaki içerik bozulmasını yakalamalı");
    File.Copy(Path.Combine(tempSource, "büyük-dosya.bin"), corruptedDestination, overwrite: true);

    var repeatResult = await runner.RunAsync(job);
    Assert(repeatResult.IsSuccessful, "Aynı iş ikinci kez güvenle çalışabilmeli");

    var repeatedAnalysis = await analyzer.AnalyzeAsync(tempSource, tempDestination);
    Assert(repeatedAnalysis.ExistingFileCount == 101, "Tekrar analizinde mevcut dosyalar bulunmalı");
    Assert(repeatedAnalysis.Warnings.Any(), "Mevcut dosyalar için kullanıcı uyarılmalı");

    job.Status = repeatResult.Status;
    job.ExitCode = repeatResult.ExitCode;
    job.CompletedAt = DateTimeOffset.Now;
    job.Summary = repeatResult.Summary;
    var logStore = new JobLogStore(storeDirectory);
    job.LogPath = await logStore.SaveAsync(job, ["örnek robocopy satırı"]);
    Assert(File.Exists(job.LogPath), "Transfer günlüğü oluşturulmalı");
    Assert((await File.ReadAllTextAsync(job.LogPath)).Contains("örnek robocopy satırı"),
        "Transfer günlüğü Robocopy çıktısını içermeli");

    var reportJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = profile,
        Status = CopyJobStatus.CompletedWithErrors,
        Summary = "Bir öğe <hatalı>",
        FailedItemCount = 1,
        Failures = [new CopyFailure(Path.Combine(tempSource, "örnek.csv"), "Erişim \"engellendi\"", 5)]
    };
    var report = await new TransferReportService().ExportAsync(reportJob, Path.Combine(storeDirectory, "reports"));
    var reportHtml = await File.ReadAllTextAsync(report.HtmlPath);
    var reportCsv = await File.ReadAllTextAsync(report.CsvPath);
    Assert(reportHtml.Contains("&lt;") && !reportHtml.Contains("<hatalı>"),
        "HTML raporu kullanıcı verisini güvenli biçimde kodlamalı");
    Assert(reportCsv.Contains("\"Erişim \"\"engellendi\"\"\""),
        "CSV raporu tırnak karakterlerini güvenli biçimde kaçırmalı");

    var historyStore = new HistoryStore(storeDirectory);
    await historyStore.AddAsync(job);
    await historyStore.AddAsync(job);
    var history = await historyStore.LoadAsync();
    Assert(history.Count == 1 && history[0].Id == job.Id, "Geçmiş aynı işi çoğaltmadan güncellemeli");
    await historyStore.ClearAsync();
    Assert((await historyStore.LoadAsync()).Count == 0, "Transfer geçmişi temizlenebilmeli");
}
finally
{
    Directory.Delete(tempSource, true);
    if (Directory.Exists(tempDestination))
        Directory.Delete(tempDestination, true);
    if (Directory.Exists(shellStateDirectory))
        Directory.Delete(shellStateDirectory, true);
    if (Directory.Exists(storeDirectory))
        Directory.Delete(storeDirectory, true);
}

if (args.Contains("--stress", StringComparer.OrdinalIgnoreCase))
{
    var stressRoot = Path.Combine(Path.GetTempPath(), "CopyPasteStress", Guid.NewGuid().ToString("N"));
    var stressSource = Path.Combine(stressRoot, "source");
    var stressDestination = Path.Combine(stressRoot, "destination");
    var stressFileCount = int.TryParse(Environment.GetEnvironmentVariable("COPYPASTE_STRESS_FILES"), out var configured)
        ? Math.Clamp(configured, 1_000, 80_000)
        : 10_000;
    try
    {
        Directory.CreateDirectory(stressSource);
        for (var index = 0; index < stressFileCount; index++)
        {
            var folder = Path.Combine(stressSource, $"batch-{index / 500:000}");
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, $"file-{index:000000}.txt"), $"CopyPaste {index}");
        }
        var stressJob = new CopyJob
        {
            SourcePath = stressSource,
            DestinationPath = stressDestination,
            Profile = new CopyProfile("stress", "Stress", "", 32, false, 1, 1),
            EstimatedFileCount = stressFileCount
        };
        var stressAnalysis = await new CopyPreflightAnalyzer().AnalyzeAsync(stressSource, stressDestination);
        stressJob.EstimatedTotalBytes = stressAnalysis.TotalBytes;
        var stressResult = await new RobocopyRunner().RunAsync(stressJob);
        var copiedCount = Directory.Exists(stressDestination)
            ? Directory.EnumerateFiles(stressDestination, "*", SearchOption.AllDirectories).Count()
            : 0;
        Assert(stressResult.IsSuccessful && copiedCount == stressFileCount,
            $"{stressFileCount:N0} dosyalı dayanıklılık transferi eksiksiz tamamlanmalı");
    }
    finally
    {
        if (Directory.Exists(stressRoot))
            Directory.Delete(stressRoot, recursive: true);
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("CopyPaste.Core: tüm kontroller başarılı.");
return 0;

void Assert(bool condition, string message)
{
    if (!condition)
        failures.Add("BAŞARISIZ: " + message);
}

sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json"),
        RequestMessage = request
    });
}

sealed class ByteStubHttpMessageHandler(byte[] content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content),
        RequestMessage = request
    });
}

sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
