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
      "browser_download_url": "https://github.com/EddizEge/CopyPaste/releases/download/v1.2.0/CopyPaste-1.2.0-Setup.exe"
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
       && availableUpdate.DownloadUri?.AbsoluteUri.EndsWith("CopyPaste-1.2.0-Setup.exe") == true,
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

    var profile = CopyProfiles.All[0];
    var job = new CopyJob { SourcePath = tempSource, DestinationPath = tempDestination, Profile = profile };
    var info = RobocopyCommandBuilder.Build(job);
    Assert(info.FileName.EndsWith("robocopy.exe", StringComparison.OrdinalIgnoreCase), "Robocopy çalıştırılmalı");
    Assert(info.ArgumentList.Contains("/MT:16"), "Profil thread sayısı uygulanmalı");
    Assert(!info.ArgumentList.Contains("/MIR"), "Tehlikeli aynalama varsayılan olmamalı");

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
        MinimizeToTrayWhileRunning = false
    };
    await settingsStore.SaveAsync(savedSettings);
    Assert(await settingsStore.LoadAsync() == savedSettings, "Uygulama ayarları kalıcı olmalı");

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

    var textOnlyOptions = new CopyJobOptions { FilePatterns = ["*.txt"] };
    var filteredAnalysis = await analyzer.AnalyzeAsync(tempSource, tempDestination, textOnlyOptions);
    Assert(filteredAnalysis.FileCount == 100, "Ön analiz dosya filtresini uygulamalı");
    Assert(filteredAnalysis.TotalBytes < initialAnalysis.TotalBytes, "Filtreli analiz yalnızca seçilen dosyaların boyutunu toplamalı");

    var runner = new RobocopyRunner();
    var result = await runner.RunAsync(job);
    Assert(result.IsSuccessful, $"Gerçek Robocopy transferi başarılı olmalı (kod: {result.ExitCode})");

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
        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var livePartialResult = await runner.RunAsync(partialJob);
            Assert(livePartialResult.Status == CopyJobStatus.CompletedWithErrors,
                $"Gerçek Robocopy kısmi hatası doğru sınıflanmalı (kod: {livePartialResult.ExitCode})");
            Assert(livePartialResult.FailedItemCount >= 1 && livePartialResult.Failures?.Count >= 1,
                "Gerçek Robocopy çıktısından kilitli dosya ayrıntısı alınmalı");
        }
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
