using CopyPaste.Core.Models;
using CopyPaste.Core.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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
Assert(ReleaseNotesCatalog.All is [{ Version: "1.7.6", IsPreview: false }, { Version: "1.7.5", IsPreview: false },
           { Version: "1.7.0", IsPreview: false }, ..]
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

var transferPathCases = new (string? Path, TransferPathKind Expected)[]
{
    (null, TransferPathKind.Empty),
    ("", TransferPathKind.Empty),
    ("   ", TransferPathKind.Empty),
    (@"C:\Foto", TransferPathKind.FileSystem),
    (@"D:\", TransferPathKind.FileSystem),
    (@"\\NAS\paylaşım\x", TransferPathKind.FileSystem),
    (@"\\?\C:\uzun\yol", TransferPathKind.FileSystem),
    (@"\\?\UNC\NAS\p", TransferPathKind.FileSystem),
    (@"  E:\Yedek\Tatil 2026  ", TransferPathKind.FileSystem),
    (@"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\?\usb#vid_05ac&pid_12a8#0000#{6ac27878-a6fa-4155-ba85-f98f491d4f33}\SID-{10001,,1}\DCIM",
        TransferPathKind.PortableDevice),
    (@"\\?\usb#vid_05ac&pid_12a8#00008110000000000000#{6ac27878-a6fa-4155-ba85-f98f491d4f33}", TransferPathKind.PortableDevice),
    (@"\\?\swd#wpdbusenum#_??_usbstor#disk&ven_samsung#{6ac27878-a6fa-4155-ba85-f98f491d4f33}", TransferPathKind.PortableDevice),
    ("shell:MyComputerFolder", TransferPathKind.ShellNamespace),
    ("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", TransferPathKind.ShellNamespace),
    (@"Bu bilgisayar\Apple iPhone\Internal Storage", TransferPathKind.PortableDevice),
    (@"This PC\Apple iPhone\Internal Storage\DCIM", TransferPathKind.PortableDevice),
    (@"This PC\Local Disk (C:)\Foto", TransferPathKind.ShellNamespace),
    ("This PC", TransferPathKind.ShellNamespace),
    (@"Foto\Tatil", TransferPathKind.NotRooted),
    (@"C:Foto", TransferPathKind.NotRooted),
    (@"\Foto", TransferPathKind.NotRooted)
};
foreach (var (path, expected) in transferPathCases)
{
    var actual = TransferPathClassifier.Classify(path);
    Assert(actual == expected,
        $"Transfer yolu sınıflandırması '{path ?? "<null>"}' için {expected} olmalı, {actual} bulundu");
}

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
      "browser_download_url": "https://github.com/EddizEge/CopyPaste/releases/download/v1.2.0/CopyPaste-1.2.0-win-x64.zip",
      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
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
var portableUpdateService = new GitHubUpdateService(
    new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, releaseJson)),
    preferPortable: true);
var portableUpdate = await portableUpdateService.CheckAsync(new Version(1, 1, 0));
Assert(portableUpdate.HasUpdate
       && portableUpdate.DownloadUri?.AbsoluteUri.EndsWith("CopyPaste-1.2.0-win-x64.zip") == true
       && portableUpdate.AssetName == "CopyPaste-1.2.0-win-x64.zip"
       && portableUpdate.Sha256Digest?.EndsWith(new string('b', 64), StringComparison.Ordinal) == true,
    "Portable çalışmada ZIP paketi Setup dosyasına tercih edilmeli");
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
var deletionTestRoot = tempSource + "-deletion-tests";
Directory.CreateDirectory(tempSource);
var portableDetectionRoot = tempSource + "-portable-detection";
Directory.CreateDirectory(portableDetectionRoot);
Assert(!PortableDistributionDetector.IsPortable(portableDetectionRoot),
    "Dağıtım işareti bulunmayan klasör kurulu çalışma kabul edilmeli");
await File.WriteAllTextAsync(
    Path.Combine(portableDetectionRoot, ProductInfo.PortableMarkerFileName), "portable");
Assert(PortableDistributionDetector.IsPortable(portableDetectionRoot),
    "Portable dağıtım işareti bulunan klasör portable çalışma olarak algılanmalı");

try
{
    var deletionRoot = Path.Combine(deletionTestRoot, "silme-araçları-ğüşi");
    var deletionProtected = Path.Combine(deletionTestRoot, "protected-root");
    var deletionApp = Path.Combine(deletionTestRoot, "running-app");
    Directory.CreateDirectory(deletionRoot);
    Directory.CreateDirectory(deletionProtected);
    Directory.CreateDirectory(deletionApp);
    var nestedDeletionDirectory = Path.Combine(deletionRoot, "iç içe", "alt");
    Directory.CreateDirectory(nestedDeletionDirectory);
    var firstDeletionFile = Path.Combine(deletionRoot, "rapor-çözüldü.txt");
    var nestedDeletionFile = Path.Combine(nestedDeletionDirectory, "uzun-adlı-dosya.bin");
    await File.WriteAllTextAsync(firstDeletionFile, "CopyPaste deletion test");
    await File.WriteAllBytesAsync(nestedDeletionFile, new byte[4097]);
    var deletionPlanner = new DeletionPlanner(deletionProtected, deletionApp);
    var permanentPlan = deletionPlanner.Create(
        [deletionRoot, nestedDeletionDirectory], DeletionMode.Permanent);
    Assert(permanentPlan.IsValid
           && permanentPlan.RequestedPaths.SequenceEqual([deletionRoot], StringComparer.OrdinalIgnoreCase)
           && permanentPlan.FileCount == 2
           && permanentPlan.DirectoryCount == 3
           && permanentPlan.TotalBytes == new FileInfo(firstDeletionFile).Length + 4097,
        "Silme önizlemesi iç içe seçimleri tekilleştirip Unicode dosya ve klasörleri saymalı");
    var recursiveDeleteResult = await new DeletionService(deletionPlanner).ExecuteAsync(permanentPlan);
    Assert(recursiveDeleteResult.Succeeded && !Directory.Exists(deletionRoot),
        "Kalıcı silme önizlenmiş geçici klasör ağacını revalidation sonrasında alttan üste silebilmeli");

    var protectedFile = Path.Combine(deletionProtected, "korunan.txt");
    await File.WriteAllTextAsync(protectedFile, "protected");
    var protectedPlan = deletionPlanner.Create([protectedFile], DeletionMode.Permanent);
    Assert(!protectedPlan.IsValid
           && protectedPlan.Issues.Any(issue => issue.Code == DeletionIssueCode.ProtectedPath),
        "Silme önizlemesi korunan dizinlerin altındaki hedefleri reddetmeli");

    var rootPlan = deletionPlanner.Create([Path.GetPathRoot(tempSource)!], DeletionMode.Permanent);
    Assert(!rootPlan.IsValid && rootPlan.Issues.Any(issue => issue.Code == DeletionIssueCode.VolumeRoot),
        "Silme önizlemesi birim köklerini reddetmeli");

    var changedFile = Path.Combine(deletionTestRoot, "preview-sonrası-değişti.bin");
    await File.WriteAllBytesAsync(changedFile, [1, 2, 3]);
    var changedPlan = deletionPlanner.Create([changedFile], DeletionMode.Permanent);
    var changedPlanFingerprint = DeletionPlanFingerprint.Create(changedPlan);
    await File.AppendAllTextAsync(changedFile, "changed after preview");
    var refreshedChangedPlan = deletionPlanner.Create([changedFile], DeletionMode.Permanent);
    var changedResult = await new DeletionService(deletionPlanner).ExecuteAsync(changedPlan);
    Assert(changedResult.FailedCount == 1
           && changedResult.Items[0].ErrorCode == DeletionIssueCode.ChangedAfterPreview
           && File.Exists(changedFile),
        "Önizlemeden sonra değişen hedef yeniden doğrulanmalı ve silinmemeli");
    Assert(changedPlanFingerprint != DeletionPlanFingerprint.Create(refreshedChangedPlan),
        "UAC aracısına aktarılan silme planı parmak izi önizleme sonrası değişiklikleri yakalamalı");

    var elevatedDirectory = Path.Combine(deletionTestRoot, "uac-plan-drift");
    Directory.CreateDirectory(elevatedDirectory);
    await File.WriteAllTextAsync(Path.Combine(elevatedDirectory, "önizlenen.txt"), "first");
    var elevatedPlan = deletionPlanner.Create([elevatedDirectory], DeletionMode.Permanent);
    var elevatedPlanFingerprint = DeletionPlanFingerprint.Create(elevatedPlan);
    await File.WriteAllTextAsync(Path.Combine(elevatedDirectory, "sonradan-eklenen.txt"), "second");
    var refreshedElevatedPlan = deletionPlanner.Create([elevatedDirectory], DeletionMode.Permanent);
    Assert(elevatedPlanFingerprint != DeletionPlanFingerprint.Create(refreshedElevatedPlan)
           && Directory.Exists(elevatedDirectory),
        "UAC öncesi klasöre eklenen yeni hedef yükseltilmiş işlem planını geçersiz kılmalı");

    var permanentFile = Path.Combine(deletionTestRoot, "salt-okunur.txt");
    await File.WriteAllTextAsync(permanentFile, "delete me");
    File.SetAttributes(permanentFile, File.GetAttributes(permanentFile) | FileAttributes.ReadOnly);
    var directDeletePlan = deletionPlanner.Create([permanentFile], DeletionMode.Permanent);
    var directDeleteResult = await new DeletionService(deletionPlanner).ExecuteAsync(directDeletePlan);
    Assert(directDeleteResult.Succeeded && !File.Exists(permanentFile),
        "Kalıcı silme yalnızca seçilmiş geçici salt-okunur dosyanın özniteliğini temizleyip silebilmeli");

    var lockedFile = Path.Combine(deletionTestRoot, "kullanımda.txt");
    await File.WriteAllTextAsync(lockedFile, "locked");
    var lockedPlan = deletionPlanner.Create([lockedFile], DeletionMode.Permanent);
    await using (var lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        var lockedResult = await new DeletionService(deletionPlanner).ExecuteAsync(lockedPlan);
        Assert(lockedResult.FailedCount == 1
               && lockedResult.Items[0].ErrorCode == DeletionIssueCode.InUse
               && File.Exists(lockedFile),
            "Kullanımdaki dosya zorla kapatılmadan öğe bazlı hata olarak raporlanmalı");
    }

    var hardLinkTarget = Path.Combine(deletionTestRoot, "hard-link-target.bin");
    var hardLinkAlias = Path.Combine(deletionTestRoot, "hard-link-alias.bin");
    await File.WriteAllBytesAsync(hardLinkTarget, [4, 5, 6, 7]);
    Assert(NativeFileMethods.CreateHardLink(hardLinkAlias, hardLinkTarget, IntPtr.Zero),
        "Hard link güvenlik testi için geçici bağlantı oluşturulabilmeli");
    if (File.Exists(hardLinkAlias))
    {
        var hardLinkPlan = deletionPlanner.Create([hardLinkTarget], DeletionMode.SecureErase);
        Assert(!hardLinkPlan.IsValid
               && hardLinkPlan.Issues.Any(issue => issue.Code == DeletionIssueCode.MultipleHardLinks),
            "Güvenli silme başka bir yolu da bozabilecek hard link hedeflerini reddetmeli");
    }

    var reparseOutside = Path.Combine(deletionTestRoot, "reparse-outside");
    var reparseSelectedRoot = Path.Combine(deletionTestRoot, "reparse-selected");
    var junctionPath = Path.Combine(reparseSelectedRoot, "linked-folder");
    var outsideFile = Path.Combine(reparseOutside, "outside-must-survive.txt");
    Directory.CreateDirectory(reparseOutside);
    Directory.CreateDirectory(reparseSelectedRoot);
    await File.WriteAllTextAsync(outsideFile, "must not be traversed");
    Assert(NativeFileMethods.CreateJunction(junctionPath, reparseOutside),
        "Reparse-point sınır testi için geçici junction oluşturulabilmeli");
    if (Directory.Exists(junctionPath)
        && (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0)
    {
        var junctionPlan = deletionPlanner.Create([reparseSelectedRoot], DeletionMode.Permanent);
        Assert(junctionPlan.IsValid
               && junctionPlan.Entries.Any(entry => entry.Path == junctionPath
                   && entry.Kind == DeletionEntryKind.ReparsePoint)
               && junctionPlan.Entries.All(entry => !entry.Path.Equals(
                   outsideFile, StringComparison.OrdinalIgnoreCase)),
            "Silme önizlemesi junction hedefinin içine girmeden bağlantıyı sınır olarak planlamalı");
        var junctionResult = await new DeletionService(deletionPlanner).ExecuteAsync(junctionPlan);
        Assert(junctionResult.Succeeded
               && !Directory.Exists(reparseSelectedRoot)
               && File.Exists(outsideFile),
            "Kalıcı silme junction bağlantısını kaldırmalı ancak bağlantı dışındaki hedef veriye dokunmamalı");
    }

    var secureFile = Path.Combine(deletionTestRoot, "güvenli-silme.bin");
    await File.WriteAllBytesAsync(secureFile, RandomNumberGenerator.GetBytes(1024 * 1024 + 17));
    var securePlan = deletionPlanner.Create([secureFile], DeletionMode.SecureErase);
    Assert(securePlan.IsValid && securePlan.FileCount == 1,
        "Yerel sabit NTFS birimindeki normal dosya güvenli silme için planlanabilmeli");
    if (securePlan.IsValid)
    {
        var secureProgressSeen = false;
        var secureResult = await new DeletionService(deletionPlanner).ExecuteAsync(
            securePlan, new InlineProgress<DeletionProgress>(_ => secureProgressSeen = true));
        Assert(secureResult.Succeeded && secureProgressSeen && !File.Exists(secureFile),
            "Güvenli silme geçici dosyayı akış halinde üzerine yazıp kaldırmalı");
    }


    var cancelDuringOverwriteFile = Path.Combine(deletionTestRoot, "üzerine-yazarken-iptal.bin");
    await File.WriteAllBytesAsync(cancelDuringOverwriteFile, new byte[8 * 1024 * 1024]);
    var cancelDuringOverwritePlan = deletionPlanner.Create(
        [cancelDuringOverwriteFile], DeletionMode.SecureErase);
    if (cancelDuringOverwritePlan.IsValid)
    {
        using var overwriteCancellation = new CancellationTokenSource();
        var cancelDuringOverwriteResult = await new DeletionService(deletionPlanner).ExecuteAsync(
            cancelDuringOverwritePlan,
            new InlineProgress<DeletionProgress>(value =>
            {
                if (value.Pass == 1 && value.ProcessedBytes > 0)
                    overwriteCancellation.Cancel();
            }),
            overwriteCancellation.Token);
        Assert(cancelDuringOverwriteResult.WasCanceled
               && cancelDuringOverwriteResult.Items is [{ Succeeded: false, PartiallyOverwritten: true }]
               && File.Exists(cancelDuringOverwriteFile),
            "Güvenli silme sırasında iptal dosyayı silmemeli ve kısmi üzerine yazmayı açıkça raporlamalı");
    }

    var canceledFile = Path.Combine(deletionTestRoot, "iptal-edildi.txt");
    await File.WriteAllTextAsync(canceledFile, "keep me");
    var canceledPlan = deletionPlanner.Create([canceledFile], DeletionMode.Permanent);
    using (var canceledSource = new CancellationTokenSource())
    {
        canceledSource.Cancel();
        var canceledResult = await new DeletionService(deletionPlanner)
            .ExecuteAsync(canceledPlan, cancellationToken: canceledSource.Token);
        Assert(canceledResult.WasCanceled && File.Exists(canceledFile),
            "Başlamadan iptal edilen silme işlemi hedefe dokunmamalı");
    }

    var selectionState = new DeletionSelectionState();
    Assert(selectionState.TryAdd("  \"C:\\Seçim\\Bir\"  ", out var firstSelection)
           && firstSelection == @"C:\Seçim\Bir"
           && !selectionState.TryAdd(@"c:\seçim\bir", out _)
           && selectionState.Count == 1,
        "Silme seçimi yolları kırpıp büyük/küçük harf duyarsız tekilleştirmeyi korumalı");
    var unchangedSnapshot = selectionState.Snapshot();
    Assert(!selectionState.TryAdd(@"C:\SEÇim\BİR".Replace("İ", "I"), out _)
           && selectionState.IsCurrent(unchangedSnapshot.Version),
        "Silme seçimi değişmediyse önizleme anlık görüntüsü güncel kalmalı");
    selectionState.Clear();
    selectionState.TryAdd(@"C:\Seçim\Bir", out _);
    var addSnapshot = selectionState.Snapshot();
    selectionState.TryAdd(@"C:\Seçim\İki", out _);
    Assert(!selectionState.IsCurrent(addSnapshot.Version)
           && addSnapshot.Paths.SequenceEqual([@"C:\Seçim\Bir"]),
        "Önizleme sırasında eklenen yol eski anlık görüntüyü geçersiz kılmalı ve kopyayı değiştirmemeli");
    var removeSnapshot = selectionState.Snapshot();
    selectionState.Remove(@"C:\Seçim\Bir");
    Assert(!selectionState.IsCurrent(removeSnapshot.Version)
           && removeSnapshot.Paths.SequenceEqual([@"C:\Seçim\Bir", @"C:\Seçim\İki"]),
        "Önizleme sırasında kaldırılan yol eski anlık görüntüyü geçersiz kılmalı ve kopyayı değiştirmemeli");
    var clearSnapshot = selectionState.Snapshot();
    selectionState.Clear();
    Assert(!selectionState.IsCurrent(clearSnapshot.Version)
           && clearSnapshot.Paths.SequenceEqual([@"C:\Seçim\İki"])
           && selectionState.Count == 0,
        "Önizleme sırasında temizlenen liste eski anlık görüntüyü geçersiz kılmalı");
    var idleSnapshot = selectionState.Snapshot();
    selectionState.Clear();
    Assert(!selectionState.Remove(@"C:\Yok") && !selectionState.TryAdd("  ", out _)
           && selectionState.IsCurrent(idleSnapshot.Version),
        "Etkisiz seçim işlemleri önizleme sürümünü değiştirmemeli");

    var validFingerprint = new string('a', 64);
    var validCancelFile = @"C:\CopyPaste\DeletionRequests\x.cancel";
    ElevatedDeletionRequest BrokerRequest(IReadOnlyList<string> paths, DeletionMode mode, string fingerprint) =>
        new(paths, mode, fingerprint, validCancelFile);
    DeletionElevationRequestError BrokerError(ElevatedDeletionRequest? request) =>
        DeletionElevationRequestValidator.Validate(request).Error;
    Assert(BrokerError(null) == DeletionElevationRequestError.MissingRequest
           && BrokerError(BrokerRequest(null!, DeletionMode.Permanent, validFingerprint))
               == DeletionElevationRequestError.MissingPaths
           && BrokerError(BrokerRequest([], DeletionMode.Permanent, validFingerprint))
               == DeletionElevationRequestError.MissingPaths
           && BrokerError(BrokerRequest([@"C:\a", null!], DeletionMode.Permanent, validFingerprint))
               == DeletionElevationRequestError.InvalidPathEntry
           && BrokerError(BrokerRequest([@"C:\a", " "], DeletionMode.Permanent, validFingerprint))
               == DeletionElevationRequestError.InvalidPathEntry
           && BrokerError(BrokerRequest(
                   Enumerable.Repeat(@"C:\a", DeletionElevationRequestValidator.MaximumPathCount + 1).ToArray(),
                   DeletionMode.Permanent, validFingerprint))
               == DeletionElevationRequestError.TooManyPaths,
        "UAC aracısı boş, null veya aşırı büyük hedef listelerini reddetmeli");
    Assert(BrokerError(BrokerRequest([@"C:\a"], (DeletionMode)99, validFingerprint))
               == DeletionElevationRequestError.InvalidMode
           && BrokerError(BrokerRequest([@"C:\a"], (DeletionMode)(-1), validFingerprint))
               == DeletionElevationRequestError.InvalidMode
           && BrokerError(BrokerRequest([@"C:\a"], DeletionMode.RecycleBin, validFingerprint))
               == DeletionElevationRequestError.InvalidMode
           && BrokerError(BrokerRequest([@"C:\a"], (DeletionMode)0, validFingerprint))
               == DeletionElevationRequestError.InvalidMode,
        "UAC aracısı tanımsız modları ve Geri Dönüşüm Kutusu modunu kalıcı silme gibi işlememeli");
    Assert(BrokerError(BrokerRequest([@"C:\a"], DeletionMode.Permanent, null!))
               == DeletionElevationRequestError.InvalidFingerprint
           && BrokerError(BrokerRequest([@"C:\a"], DeletionMode.Permanent, ""))
               == DeletionElevationRequestError.InvalidFingerprint
           && BrokerError(BrokerRequest([@"C:\a"], DeletionMode.Permanent, "abc123"))
               == DeletionElevationRequestError.InvalidFingerprint
           && BrokerError(BrokerRequest([@"C:\a"], DeletionMode.Permanent, new string('g', 64)))
               == DeletionElevationRequestError.InvalidFingerprint
           && BrokerError(new ElevatedDeletionRequest([@"C:\a"], DeletionMode.Permanent, validFingerprint, null!))
               == DeletionElevationRequestError.InvalidCancelFile,
        "UAC aracısı boş, kısa veya hex olmayan plan parmak izlerini ve iptal yolunu reddetmeli");
    Assert(DeletionElevationRequestValidator.Validate(
               BrokerRequest([@"C:\a"], DeletionMode.Permanent, validFingerprint)).IsValid
           && DeletionElevationRequestValidator.Validate(
               BrokerRequest([@"C:\a", @"D:\b"], DeletionMode.SecureErase, validFingerprint.ToUpperInvariant())).IsValid,
        "UAC aracısı geçerli kalıcı ve güvenli silme isteklerini kabul etmeli");
    var brokerJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    ElevatedDeletionRequest? FromBrokerJson(string json) =>
        JsonSerializer.Deserialize<ElevatedDeletionRequest>(json, brokerJsonOptions);
    var brokerFingerprintJson = JsonSerializer.Serialize(validFingerprint);
    var brokerCancelJson = JsonSerializer.Serialize(validCancelFile);
    Assert(BrokerError(FromBrokerJson(
               $"{{\"paths\":[\"C:\\\\a\"],\"mode\":99,\"planFingerprint\":{brokerFingerprintJson},\"cancelFile\":{brokerCancelJson},\"unknown\":true}}"))
               == DeletionElevationRequestError.InvalidMode
           && BrokerError(FromBrokerJson(
               $"{{\"mode\":1,\"planFingerprint\":{brokerFingerprintJson},\"cancelFile\":{brokerCancelJson}}}"))
               == DeletionElevationRequestError.MissingPaths
           && BrokerError(FromBrokerJson(
               $"{{\"paths\":[null],\"mode\":1,\"planFingerprint\":{brokerFingerprintJson},\"cancelFile\":{brokerCancelJson}}}"))
               == DeletionElevationRequestError.InvalidPathEntry
           && BrokerError(FromBrokerJson(
               $"{{\"paths\":[\"C:\\\\a\"],\"mode\":0,\"planFingerprint\":{brokerFingerprintJson},\"cancelFile\":{brokerCancelJson}}}"))
               == DeletionElevationRequestError.InvalidMode
           && BrokerError(FromBrokerJson(
               $"{{\"paths\":[\"C:\\\\a\"],\"mode\":2,\"planFingerprint\":{brokerFingerprintJson},\"cancelFile\":{brokerCancelJson},\"unknown\":1}}"))
               == DeletionElevationRequestError.None,
        "JSON'dan gelen sayısal mod, eksik liste ve null öğe UAC aracısı doğrulayıcısından geçmeli");
    var brokerRoundTripFile = Path.Combine(deletionTestRoot, "aracı-gidiş-dönüş.txt");
    await File.WriteAllTextAsync(brokerRoundTripFile, "broker");
    var brokerRoundTripPlan = deletionPlanner.Create([brokerRoundTripFile], DeletionMode.Permanent);
    var brokerRoundTrip = FromBrokerJson(JsonSerializer.Serialize(new ElevatedDeletionRequest(
        brokerRoundTripPlan.RequestedPaths, brokerRoundTripPlan.Mode,
        DeletionPlanFingerprint.Create(brokerRoundTripPlan), validCancelFile), brokerJsonOptions));
    Assert(DeletionElevationRequestValidator.Validate(brokerRoundTrip).IsValid,
        "Orta bütünlüklü tarafın yazdığı gerçek UAC isteği doğrulayıcıdan geçmeli");

    Assert(!DeletionPlanner.IsSecureEraseLinkCountAcceptable(null)
           && DeletionPlanner.IsSecureEraseLinkCountAcceptable(1)
           && !DeletionPlanner.IsSecureEraseLinkCountAcceptable(2)
           && !DeletionPlanner.IsSecureEraseLinkCountAcceptable(0),
        "Güvenli silme yalnızca hard link sayısı tam olarak 1 okunabilen dosyalara izin vermeli");

    var lateLinkTarget = Path.Combine(deletionTestRoot, "sonradan-bağlanan.bin");
    var lateLinkAlias = Path.Combine(deletionTestRoot, "sonradan-bağlanan-diğer-yol.bin");
    var lateLinkCompanion = Path.Combine(deletionTestRoot, "sonradan-bağlanan-yanındaki.bin");
    var lateLinkContent = RandomNumberGenerator.GetBytes(64 * 1024 + 3);
    await File.WriteAllBytesAsync(lateLinkTarget, lateLinkContent);
    await File.WriteAllBytesAsync(lateLinkCompanion, RandomNumberGenerator.GetBytes(4096));
    var lateLinkPlan = deletionPlanner.Create([lateLinkTarget, lateLinkCompanion], DeletionMode.SecureErase);
    Assert(lateLinkPlan.IsValid, "Hard link eklenmeden önce güvenli silme planı geçerli olmalı");
    if (lateLinkPlan.IsValid
        && NativeFileMethods.CreateHardLink(lateLinkAlias, lateLinkTarget, IntPtr.Zero))
    {
        var lateLinkEntry = lateLinkPlan.Entries.Single(entry => entry.Path == lateLinkTarget);
        // Creating a hard link changes neither size, attributes nor last-write time, so the
        // preview revalidation still accepts the entry; only the service's own link recheck can stop it.
        Assert(deletionPlanner.IsEntryUnchanged(lateLinkEntry, out _),
            "Sonradan eklenen hard link önizleme yeniden doğrulamasını geçmeli (servis içi kontrolün kanıtı)");
        var lateLinkResult = await new DeletionService(deletionPlanner).ExecuteAsync(lateLinkPlan);
        var lateLinkItem = lateLinkResult.Items.SingleOrDefault(item => item.Path == lateLinkTarget);
        var companionItem = lateLinkResult.Items.SingleOrDefault(item => item.Path == lateLinkCompanion);
        Assert(lateLinkItem is { Succeeded: false, PartiallyOverwritten: false,
                   ErrorCode: DeletionIssueCode.MultipleHardLinks }
               && File.Exists(lateLinkTarget) && File.Exists(lateLinkAlias)
               && (await File.ReadAllBytesAsync(lateLinkTarget)).SequenceEqual(lateLinkContent)
               && (await File.ReadAllBytesAsync(lateLinkAlias)).SequenceEqual(lateLinkContent),
            "Önizlemeden sonra eklenen hard link güvenli silmede üzerine yazılmadan reddedilmeli");
        Assert(companionItem is { Succeeded: true } && !File.Exists(lateLinkCompanion),
            "Hard link nedeniyle reddedilen dosya diğer güvenli silme hedeflerini durdurmamalı");
    }

    var readOnlySecureFile = Path.Combine(deletionTestRoot, "salt-okunur-güvenli.bin");
    await File.WriteAllBytesAsync(readOnlySecureFile, RandomNumberGenerator.GetBytes(8192));
    File.SetAttributes(readOnlySecureFile, File.GetAttributes(readOnlySecureFile) | FileAttributes.ReadOnly);
    var readOnlySecurePlan = deletionPlanner.Create([readOnlySecureFile], DeletionMode.SecureErase);
    if (readOnlySecurePlan.IsValid)
    {
        var readOnlySecureResult = await new DeletionService(deletionPlanner).ExecuteAsync(readOnlySecurePlan);
        Assert(readOnlySecureResult.Succeeded && !File.Exists(readOnlySecureFile)
               && readOnlySecureResult.Items.All(item => item.ErrorCode != DeletionIssueCode.AccessDenied),
            "Güvenli silme salt okunur geçici dosyada AccessDenied vermeden üzerine yazıp silmeli");
    }
    else
    {
        Assert(false, "Salt okunur geçici dosya güvenli silme için planlanabilmeli");
    }
    if (File.Exists(readOnlySecureFile))
        File.SetAttributes(readOnlySecureFile, FileAttributes.Normal);

    var readOnlyLockedFile = Path.Combine(deletionTestRoot, "salt-okunur-kilitli.bin");
    var readOnlyLockedContent = RandomNumberGenerator.GetBytes(4096);
    await File.WriteAllBytesAsync(readOnlyLockedFile, readOnlyLockedContent);
    File.SetAttributes(readOnlyLockedFile, File.GetAttributes(readOnlyLockedFile) | FileAttributes.ReadOnly);
    var readOnlyLockedPlan = deletionPlanner.Create([readOnlyLockedFile], DeletionMode.SecureErase);
    if (readOnlyLockedPlan.IsValid)
    {
        DeletionResult readOnlyLockedResult;
        await using (new FileStream(readOnlyLockedFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            readOnlyLockedResult = await new DeletionService(deletionPlanner).ExecuteAsync(readOnlyLockedPlan);
        Assert(readOnlyLockedResult.Items is [{ Succeeded: false, PartiallyOverwritten: false,
                   ErrorCode: DeletionIssueCode.InUse }]
               && (File.GetAttributes(readOnlyLockedFile) & FileAttributes.ReadOnly) != 0
               && (await File.ReadAllBytesAsync(readOnlyLockedFile)).SequenceEqual(readOnlyLockedContent),
            "Üzerine yazma açılamazsa salt okunur öznitelik geri yüklenmeli ve içerik değişmemeli");
    }
    if (File.Exists(readOnlyLockedFile))
        File.SetAttributes(readOnlyLockedFile, FileAttributes.Normal);

    // Geri Dönüşüm Kutusu: sahte (salt okur) Kutu durumu, saf kabuk kararları ve servis sonuçları.
    var recycleRoot = Path.Combine(deletionTestRoot, "geri-dönüşüm-kutusu");
    Directory.CreateDirectory(recycleRoot);
    var recycleFile = Path.Combine(recycleRoot, "kutu-dosyası.bin");
    await File.WriteAllBytesAsync(recycleFile, new byte[4097]);
    var recycleFolder = Path.Combine(recycleRoot, "kutu-klasörü");
    Directory.CreateDirectory(recycleFolder);
    await File.WriteAllBytesAsync(Path.Combine(recycleFolder, "bir.bin"), new byte[3000]);
    await File.WriteAllBytesAsync(Path.Combine(recycleFolder, "iki.bin"), new byte[3000]);
    const string fakeRecycleDirectory = @"C:\$Recycle.Bin\S-1-5-21-1-2-3-1001";
    RecycleBinVolumeStatus HealthyRecycleBin() => new(false, false, 1L << 40, fakeRecycleDirectory);
    DeletionPlanner RecyclePlanner(Func<string, RecycleBinVolumeStatus> status) =>
        new(deletionProtected, deletionApp, new FakeRecycleBinStatusProvider(status));
    DeletionIssueCode[] RecycleIssues(Func<string, RecycleBinVolumeStatus> status, params string[] paths) =>
        RecyclePlanner(status).Create(paths, DeletionMode.RecycleBin).Issues.Select(issue => issue.Code).ToArray();
    async Task<string> RecycleTestFile(string name, int length = 100)
    {
        var path = Path.Combine(recycleRoot, name);
        await File.WriteAllBytesAsync(path, new byte[length]);
        return path;
    }

    Assert(RecyclePlanner(_ => HealthyRecycleBin()).Create([recycleFile, recycleFolder], DeletionMode.RecycleBin).IsValid,
        "Açık ve yeterli Geri Dönüşüm Kutusu ile plan geçerli olmalı");
    Assert(RecycleIssues(_ => HealthyRecycleBin() with { NukeOnDelete = true }, recycleFile)
               is [DeletionIssueCode.RecycleBinDisabled]
           && RecycleIssues(_ => HealthyRecycleBin() with { DisabledByPolicy = true }, recycleFile)
               is [DeletionIssueCode.RecycleBinDisabled],
        "Kutu sürücüde (NukeOnDelete) ya da ilkeyle kapalıysa plan RecycleBinDisabled ile engellenmeli");
    Assert(RecycleIssues(_ => HealthyRecycleBin() with { MaxCapacityBytes = 4096 }, recycleFile)
               is [DeletionIssueCode.ExceedsRecycleBinCapacity]
           && RecycleIssues(_ => HealthyRecycleBin() with { MaxCapacityBytes = 4097 }, recycleFile).Length == 0
           && RecycleIssues(_ => HealthyRecycleBin() with { MaxCapacityBytes = 5000 }, recycleFolder)
               is [DeletionIssueCode.ExceedsRecycleBinCapacity],
        "Kökün toplam boyutu Kutu kapasitesini aşarsa plan engellenmeli; tam sığan kök engellenmemeli");
    Assert(RecycleIssues(_ => HealthyRecycleBin() with { RecycleDirectory = @"C:\$Recycle.Bin\" + new string('S', 240) },
               recycleFile) is [DeletionIssueCode.RecyclePathTooLong],
        "Kutu'daki yolu 260 karakter sınırına ulaşacak kök plan aşamasında engellenmeli");
    var longRecycleTop = Path.Combine(recycleRoot, "uzun-klasör-adı-" + new string('x', 24));
    var longRecycleDirectory = longRecycleTop;
    while (longRecycleDirectory.Length < DeletionPlanner.RecycleBinMaxPath + 10)
        longRecycleDirectory = Path.Combine(longRecycleDirectory, "uzun-klasör-adı-" + new string('x', 24));
    Directory.CreateDirectory(longRecycleDirectory);
    await File.WriteAllTextAsync(Path.Combine(longRecycleDirectory, "dosya.txt"), "uzun yol");
    Assert(RecycleIssues(_ => HealthyRecycleBin() with { RecycleDirectory = null }, longRecycleTop)
               is [DeletionIssueCode.RecyclePathTooLong],
        "Kaynak yolu 260 karakter sınırına ulaşan öğe Kutu modunda engellenmeli");
    Assert(RecycleIssues(_ => new RecycleBinVolumeStatus(null, null, null, null), recycleFile, recycleFolder).Length == 0
           && RecycleIssues(_ => throw new UnauthorizedAccessException("registry"), recycleFile).Length == 0,
        "Kutu ayarı okunamazsa plan engellenmemeli (çalışma anı koruması devrede kalır)");
    Assert(RecyclePlanner(_ => HealthyRecycleBin() with { NukeOnDelete = true })
               .Create([recycleFile], DeletionMode.Permanent).IsValid,
        "Geri Dönüşüm Kutusu ayarları kalıcı silme planını etkilememeli");

    Assert(ShellRecycleBinSender.IsRecycleDeletionAllowed(0x282)
           && ShellRecycleBinSender.IsRecycleDeletionAllowed(ShellRecycleBinSender.TransferSourceDeleteRecycleIfPossible)
           && !ShellRecycleBinSender.IsRecycleDeletionAllowed(0x2)
           && !ShellRecycleBinSender.IsRecycleDeletionAllowed(0x1002)
           && !ShellRecycleBinSender.IsRecycleDeletionAllowed(0),
        "PreDeleteItem yalnız TSF_DELETE_RECYCLE_IF_POSSIBLE bayrağı varsa silmeye izin vermeli");
    RecycleBinSendStatus Classify(bool sourceExists, bool allowed = true, bool confirmed = false,
        bool refused = false, bool canceled = false, bool permanent = false) =>
        ShellRecycleBinSender.Classify(sourceExists, allowed, confirmed, refused, canceled, permanent);
    Assert(Classify(sourceExists: false, confirmed: true) == RecycleBinSendStatus.Recycled
           && Classify(sourceExists: false) == RecycleBinSendStatus.DeletedPermanently
           && Classify(sourceExists: false, allowed: false) == RecycleBinSendStatus.Failed
           && Classify(sourceExists: false, confirmed: true, permanent: true) == RecycleBinSendStatus.DeletedPermanently
           && Classify(sourceExists: true, allowed: false, refused: true) == RecycleBinSendStatus.NotRecyclable
           && Classify(sourceExists: true, refused: true, permanent: true) == RecycleBinSendStatus.DeletedPermanently
           && Classify(sourceExists: true, allowed: false, canceled: true) == RecycleBinSendStatus.Canceled
           && Classify(sourceExists: true) == RecycleBinSendStatus.Failed,
        "Kutu sonucu yalnız Kutu'da doğrulanan öğede başarılı, Kutu'suz kaybolan öğede kalıcı silindi sayılmalı");

    var mixedPlanner = RecyclePlanner(_ => HealthyRecycleBin());
    var mixedPlan = mixedPlanner.Create([recycleFile, recycleFolder], DeletionMode.RecycleBin);
    var mixedSender = new RecordingRecycleBinSender(path =>
    {
        if (string.Equals(path, recycleFile, StringComparison.OrdinalIgnoreCase))
            return new(RecycleBinSendStatus.NotRecyclable, unchecked((int)0x80004004), 0x2, null, false);
        Directory.Delete(path, recursive: true);
        return new(RecycleBinSendStatus.Recycled, 0, 0x282, fakeRecycleDirectory + @"\$RABCDEF", false);
    });
    var mixedProgress = new List<DeletionProgress>();
    var mixedResult = await new DeletionService(mixedPlanner, mixedSender).ExecuteAsync(
        mixedPlan, new InlineProgress<DeletionProgress>(mixedProgress.Add));
    Assert(mixedResult.Items is [{ Succeeded: false, ErrorCode: DeletionIssueCode.RecycleNotPossible },
               { Succeeded: true }]
           && !mixedResult.Succeeded && !mixedResult.WasCanceled
           && File.Exists(recycleFile) && new FileInfo(recycleFile).Length == 4097
           && !Directory.Exists(recycleFolder)
           && mixedSender.Sent.Count == 2,
        "Kutu'ya gönderilemeyen öğe silinmeden başarısız raporlanmalı ve kalan köklere devam edilmeli");
    Assert(mixedProgress.Select(value => value.ProcessedBytes).SequenceEqual([4097L, 4097L + 6000])
           && mixedProgress[^1].ProcessedBytes == mixedPlan.TotalBytes
           && mixedProgress[^1].CompletedItems == 2,
        "Kutu modunda ilerleme her kök bitince o kökün bayt toplamı kadar artmalı");

    var stopFirst = await RecycleTestFile("doğrulanamayan-1.bin");
    var stopSecond = await RecycleTestFile("doğrulanamayan-2.bin");
    var stopPlanner = RecyclePlanner(_ => HealthyRecycleBin());
    var stopSender = new RecordingRecycleBinSender(path =>
    {
        File.Delete(path);
        return new(RecycleBinSendStatus.DeletedPermanently, 0, 0x282, null, false);
    });
    var stopResult = await new DeletionService(stopPlanner, stopSender).ExecuteAsync(
        stopPlanner.Create([stopFirst, stopSecond], DeletionMode.RecycleBin));
    Assert(stopResult.Items is [{ Succeeded: false, ErrorCode: DeletionIssueCode.DeletedWithoutRecycleBin },
               { Succeeded: false, ErrorCode: DeletionIssueCode.RecycleNotPossible }]
           && stopSender.Sent.Count == 1 && File.Exists(stopSecond),
        "Kutu'da doğrulanamayan silmeden sonra kalan köklere dokunulmamalı ve durum açıkça bildirilmeli");

    var canceledRecycleFile = await RecycleTestFile("iptal-kutu.bin");
    var canceledRecyclePlanner = RecyclePlanner(_ => HealthyRecycleBin());
    var canceledRecycleResult = await new DeletionService(canceledRecyclePlanner, new RecordingRecycleBinSender(_ =>
            new(RecycleBinSendStatus.Canceled, unchecked((int)0x80270000), null, null, false)))
        .ExecuteAsync(canceledRecyclePlanner.Create([canceledRecycleFile], DeletionMode.RecycleBin));
    Assert(canceledRecycleResult is { WasCanceled: true, Items.Count: 0 } && File.Exists(canceledRecycleFile),
        "Kutu gönderimi öğe değişmeden iptal edilirse sonuç iptal olmalı ve öğe yerinde kalmalı");

    var toggledStatus = HealthyRecycleBin();
    var toggleFile = await RecycleTestFile("önizlemeden-sonra-kapanan.bin");
    var togglePlanner = RecyclePlanner(_ => toggledStatus);
    var togglePlan = togglePlanner.Create([toggleFile], DeletionMode.RecycleBin);
    toggledStatus = toggledStatus with { NukeOnDelete = true };
    var toggleSender = new RecordingRecycleBinSender(_ =>
        new(RecycleBinSendStatus.Recycled, 0, 0x282, fakeRecycleDirectory + @"\$RTOGGLE", false));
    var toggleResult = await new DeletionService(togglePlanner, toggleSender).ExecuteAsync(togglePlan);
    Assert(togglePlan.IsValid
           && toggleResult.Items is [{ Succeeded: false, ErrorCode: DeletionIssueCode.RecycleBinDisabled }]
           && toggleSender.Sent.Count == 0 && File.Exists(toggleFile),
        "Önizlemeden sonra Kutu kapatılırsa Kutu gönderimi hiç başlamamalı");

    var flipStatus = HealthyRecycleBin();
    var flipFirst = await RecycleTestFile("kökler-arası-1.bin");
    var flipSecond = await RecycleTestFile("kökler-arası-2.bin");
    var flipPlanner = RecyclePlanner(_ => flipStatus);
    var flipSender = new RecordingRecycleBinSender(path =>
    {
        File.Delete(path);
        flipStatus = flipStatus with { MaxCapacityBytes = 10 };
        return new(RecycleBinSendStatus.Recycled, 0, 0x282, fakeRecycleDirectory + @"\$RFLIP01.bin", false);
    });
    var flipResult = await new DeletionService(flipPlanner, flipSender).ExecuteAsync(
        flipPlanner.Create([flipFirst, flipSecond], DeletionMode.RecycleBin));
    Assert(flipResult.Items is [{ Succeeded: true },
               { Succeeded: false, ErrorCode: DeletionIssueCode.ExceedsRecycleBinCapacity }]
           && flipSender.Sent.Count == 1 && File.Exists(flipSecond),
        "Kutu durumu her kökten hemen önce yeniden okunmalı");

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

    var updateRecoveryRoot = Path.Combine(storeDirectory, "update-recovery");
    var installedAppDirectory = Path.Combine(storeDirectory, "installed-app");
    Directory.CreateDirectory(installedAppDirectory);
    var installedExecutable = Path.Combine(installedAppDirectory, "CopyPaste.App.exe");
    var installedLibrary = Path.Combine(installedAppDirectory, "CopyPaste.Core.dll");
    await File.WriteAllTextAsync(installedExecutable, "old-executable");
    await File.WriteAllTextAsync(installedLibrary, "old-library");
    var verifiedInstaller = Path.Combine(storeDirectory, "CopyPaste-1.7.0-Setup.exe");
    await File.WriteAllTextAsync(verifiedInstaller, "verified-installer");
    var updateRecoveryService = new UpdateRecoveryService(
        updateRecoveryRoot, installedAppDirectory);
    var recoveryPreparation = await updateRecoveryService.PrepareAsync(
        installedAppDirectory, verifiedInstaller, "1.6.0", "1.7.0");
    Assert(recoveryPreparation.Success
           && recoveryPreparation.State is { Status: UpdateRecoveryStatus.Prepared }
           && File.Exists(Path.Combine(
               recoveryPreparation.State.BackupDirectory, "CopyPaste.App.exe")),
        "Güncelleme öncesinde standart kurulum için yerel geri dönüş yedeği hazırlanmalı");
    await updateRecoveryService.SetInstallTimingAsync(UpdateInstallTiming.OnExit);
    Assert((await updateRecoveryService.LoadAsync())?.InstallTiming == UpdateInstallTiming.OnExit,
        "Güncellemenin yeniden başlatma zamanı uygulama oturumları arasında korunmalı");
    var backedUpLibrary = Path.Combine(
        recoveryPreparation.State!.BackupDirectory, "CopyPaste.Core.dll");
    await File.WriteAllTextAsync(backedUpLibrary, "tampered-library");
    Assert(!await updateRecoveryService.RestoreAsync(),
        "Bütünlüğü bozulan geri dönüş yedeği uygulanmamalı");
    await File.WriteAllTextAsync(backedUpLibrary, "old-library");
    await updateRecoveryService.MarkInstallerLaunchedAsync();
    await updateRecoveryService.BeginHealthCheckAsync();
    await File.WriteAllTextAsync(installedExecutable, "new-broken-executable");
    await File.WriteAllTextAsync(installedLibrary, "new-broken-library");
    Assert(await updateRecoveryService.RestoreAsync()
           && await File.ReadAllTextAsync(installedExecutable) == "old-executable"
           && await File.ReadAllTextAsync(installedLibrary) == "old-library"
           && (await updateRecoveryService.LoadAsync())?.Status == UpdateRecoveryStatus.RolledBack,
        "Başarısız güncelleme doğrulanmış yerel yedekten geri yüklenebilmeli");
    var unsafeRecoveryPreparation = await updateRecoveryService.PrepareAsync(
        tempSource, verifiedInstaller, "1.6.0", "1.7.0");
    Assert(!unsafeRecoveryPreparation.Success,
        "Geri dönüş hazırlığı izin verilen kurulum klasörünün dışına yazmamalı");

    var recoverableJob = new CopyJob
    {
        SourcePath = tempSource,
        DestinationPath = tempDestination,
        Profile = CopyProfiles.All[0],
        BandwidthLimitMbps = 64,
        CompletionAction = CompletionAction.Sleep,
        Status = CopyJobStatus.Running,
        LastKnownBytesTransferred = 4096,
        LastKnownCompletedFiles = 2
    };
    var queueStateStore = new QueueStateStore(storeDirectory);
    await queueStateStore.SaveAsync([recoverableJob]);
    var queueLoad = await queueStateStore.LoadWithMetadataAsync();
    Assert(queueLoad.Jobs.Count == 1
           && queueLoad.Jobs[0].Status == CopyJobStatus.Paused
           && queueLoad.Jobs[0].BandwidthLimitMbps == 64
           && queueLoad.Jobs[0].CompletionAction == CompletionAction.Sleep
           && queueLoad.Jobs[0].LastKnownBytesTransferred == 4096
           && queueLoad.Jobs[0].LastKnownCompletedFiles == 2
           && queueLoad.Jobs[0].RecoveryReason == QueueRecoveryReason.UnexpectedShutdown
           && queueLoad.Jobs[0].Summary?.Contains("kurtarıldı") == true,
        "Çökme sırasında çalışan kuyruk işi duraklatılmış olarak kurtarılmalı");
    recoverableJob.LastKnownBytesTransferred = 8192;
    await queueStateStore.SaveAsync([recoverableJob]);
    await File.WriteAllTextAsync(Path.Combine(storeDirectory, "queue.json"), "{bozuk-json");
    var backupQueueLoad = await queueStateStore.LoadWithMetadataAsync();
    Assert(backupQueueLoad.UsedBackup
           && backupQueueLoad.Jobs is [{ Status: CopyJobStatus.Paused }]
           && backupQueueLoad.Jobs[0].LastKnownBytesTransferred == 4096,
        "Bozuk birincil checkpoint son geçerli atomik yedekten kurtarılmalı");
    Assert(!(await queueStateStore.LoadWithMetadataAsync()).UsedBackup,
        "Yedekten kurtarılan checkpoint birincil kaydı kendiliğinden onarmalı");
    var legacyQueueJson = JsonSerializer.Serialize(new[] { recoverableJob });
    await File.WriteAllTextAsync(Path.Combine(storeDirectory, "queue.json"), legacyQueueJson);
    var legacyQueueLoad = await queueStateStore.LoadWithMetadataAsync();
    Assert(legacyQueueLoad.MigratedLegacyFormat
           && legacyQueueLoad.Jobs is [{ Status: CopyJobStatus.Paused }],
        "1.6 ve önceki düz liste kuyruk kayıtları geriye uyumlu biçimde yüklenmeli");
    var waitingNetworkJob = new CopyJob
    {
        SourcePath = @"\\sunucu\arsiv",
        DestinationPath = tempDestination,
        Profile = CopyProfiles.All[0],
        Status = CopyJobStatus.WaitingForNetwork,
        NetworkRetryAttempt = 2,
        NetworkWaitUntil = DateTimeOffset.UtcNow.AddMinutes(5)
    };
    await queueStateStore.SaveAsync([waitingNetworkJob]);
    var recoveredNetworkQueue = await queueStateStore.LoadAsync();
    Assert(recoveredNetworkQueue is
        [{
            Status: CopyJobStatus.Paused,
            RecoveryReason: QueueRecoveryReason.NetworkUnavailable,
            NetworkRetryAttempt: 2
        }],
        "Ağ bekleme checkpoint'i yeniden başlatmada güvenli duraklatılmış işe dönüşmeli");

    var scheduleStore = new ScheduleStore(storeDirectory);
    var scheduledTransfer = new ScheduledTransfer
    {
        Name = "Gece yedeği",
        TimeOfDay = "02:30",
        Kind = ScheduleKind.Weekly,
        DayOfWeek = DayOfWeek.Saturday,
        RequireAcPower = true,
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
            RequireAcPower: true,
            Job.BandwidthLimitMbps: 32,
            Job.CompletionAction: CompletionAction.ShutDown
        },
        "Zamanlanmış transfer tüm iş seçenekleriyle saklanmalı");
    var taskArguments = TaskSchedulerCommandBuilder.BuildCreate(
        scheduledTransfer, @"C:\Program Files\CopyPaste\CopyPaste.App.exe");
    var scheduledTaskName = $@"CopyPaste\{scheduledTransfer.Id:D}";
    Assert(taskArguments.Contains("/Create")
           && taskArguments.SkipWhile(argument => argument != "/TN").Take(2)
               .SequenceEqual(["/TN", scheduledTaskName])
           && taskArguments.SkipWhile(argument => argument != "/D").Take(2)
               .SequenceEqual(["/D", "SAT"])
           && taskArguments.SkipWhile(argument => argument != "/ST").Take(2)
               .SequenceEqual(["/ST", "02:30"])
           && taskArguments.Any(argument =>
               argument == $"\"C:\\Program Files\\CopyPaste\\CopyPaste.App.exe\" --schedule {scheduledTransfer.Id:D}"),
        "Haftalık Windows görevi güvenli görev adı, saat ve alıntılanmış uygulama yoluyla oluşturulmalı");
    Assert(TaskSchedulerCommandBuilder.BuildSetEnabled(scheduledTransfer.Id, false)
               .SequenceEqual(["/Change", "/TN", scheduledTaskName, "/DISABLE"])
           && TaskSchedulerCommandBuilder.BuildSetEnabled(scheduledTransfer.Id, true)
               .SequenceEqual(["/Change", "/TN", scheduledTaskName, "/ENABLE"])
           && TaskSchedulerCommandBuilder.BuildRunNow(scheduledTransfer.Id)
               .SequenceEqual(["/Run", "/TN", scheduledTaskName])
           && TaskSchedulerCommandBuilder.BuildDelete(scheduledTransfer.Id)
               .SequenceEqual(["/Delete", "/TN", scheduledTaskName, "/F"]),
        "Zamanlanmış görev yönetim komutları yalnızca uygulamanın GUID tabanlı görev adını kullanmalı");
    var usbSchedule = scheduledTransfer with
    {
        Kind = ScheduleKind.UsbArrival,
        UsbVolumeId = "A1B2C3D4",
        UsbVolumeLabel = "YEDEK"
    };
    Assert(UsbScheduleMatcher.Matches(
               usbSchedule, new UsbDriveIdentity(@"G:\", "a1b2c3d4", "YEDEK"))
           && !UsbScheduleMatcher.Matches(
               usbSchedule, new UsbDriveIdentity(@"H:\", "FFFFFFFF", "YEDEK")),
        "USB zamanlaması sürücü harfinden bağımsız, kalıcı birim kimliğiyle eşleşmeli");
    var simulatedUsbDrive = new UsbDriveIdentity(@"G:\", "A1B2C3D4", "YEDEK");
    Assert(UsbDriveArrivalDetector.FindArrivals(
               [simulatedUsbDrive.VolumeId], [simulatedUsbDrive with { RootPath = @"H:\" }]).Count == 0,
        "Bağlı USB sürücüsünün harfi değişse bile ikinci bir varış olayı üretilmemeli");
    Assert(UsbDriveArrivalDetector.FindArrivals([simulatedUsbDrive.VolumeId], []).Count == 0,
        "USB sürücüsünün ayrılması varış olayı üretmemeli");
    var simulatedUsbRearrival = UsbDriveArrivalDetector.FindArrivals(
        [],
        [
            simulatedUsbDrive with { RootPath = @"H:\" },
            simulatedUsbDrive with { RootPath = @"I:\" }
        ]);
    Assert(simulatedUsbRearrival is [{ RootPath: @"H:\", VolumeId: "A1B2C3D4" }],
        "Ayrıldıktan sonra farklı harfle yeniden bağlanan USB sürücüsü yalnızca bir kez algılanmalı");
    Assert(UsbScheduleMatcher.Evaluate(usbSchedule, simulatedUsbDrive, false)
               == UsbScheduleTriggerDecision.AcPowerRequired
           && UsbScheduleMatcher.Evaluate(usbSchedule, simulatedUsbDrive, true)
               == UsbScheduleTriggerDecision.Ready
           && UsbScheduleMatcher.Evaluate(
               usbSchedule, new UsbDriveIdentity(@"G:\", "FFFFFFFF", "YEDEK"), true)
               == UsbScheduleTriggerDecision.NotMatched,
        "USB tetikleyicisi kalıcı kimlik ve AC güç koşulunu birlikte değerlendirmeli");
    await scheduleStore.SaveAsync(usbSchedule);
    Assert((await scheduleStore.FindAsync(usbSchedule.Id)) is
        {
            Kind: ScheduleKind.UsbArrival,
            UsbVolumeId: "A1B2C3D4",
            RequireAcPower: true
        },
        "USB birim kimliği ve AC güç koşulu zamanlama kaydında korunmalı");
    await scheduleStore.SaveAsync(scheduledTransfer);
    AssertThrows<ArgumentException>(
        () => TaskSchedulerCommandBuilder.BuildCreate(
            usbSchedule, @"C:\Program Files\CopyPaste\CopyPaste.App.exe"),
        "USB varış görevleri yanlışlıkla günlük Windows görevine dönüştürülmemeli");
    var pausedSchedule = scheduledTransfer with { Enabled = false };
    await scheduleStore.SaveAsync(pausedSchedule);
    Assert(await scheduleStore.FindAsync(pausedSchedule.Id) is null
           && (await scheduleStore.LoadAsync()).Single(schedule => schedule.Id == pausedSchedule.Id).Enabled == false,
        "Duraklatılmış görev saklanmalı ancak zamanlanmış başlatma için bulunmamalı");
    await scheduleStore.SaveAsync(scheduledTransfer);
    await scheduleStore.RemoveAsync(scheduledTransfer.Id);
    Assert(await scheduleStore.FindAsync(scheduledTransfer.Id) is null,
        "Zamanlanmış transfer kaldırılabilmeli");

    Assert(NetworkAvailabilityService.IsNetworkPath(@"\\sunucu\arsiv")
           && !NetworkAvailabilityService.IsNetworkPath(tempSource),
        "UNC ağ yolları yerel klasörlerden ayırt edilmeli");
    Assert(await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        tempSource, tempDestination, TimeSpan.FromMilliseconds(10)),
        "Yerel transferler ağ beklemesine girmemeli");
    var networkProgress = new List<NetworkWaitProgress>();
    var unavailableNetwork = await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        @"\\copy-paste-test-invalid\arsiv",
        tempDestination,
        TimeSpan.FromMilliseconds(2),
        new InlineProgress<NetworkWaitProgress>(networkProgress.Add),
        waitAsync: (_, _) => Task.CompletedTask);
    Assert(!unavailableNetwork
           && networkProgress.Count > 0
           && networkProgress.Any(progress =>
               progress.UnavailablePaths?.Contains(@"\\copy-paste-test-invalid\arsiv") == true),
        "Ağ bekleme ilerlemesi erişilemeyen yolu ve süre durumunu bildirmeli");
    var simulatedNetworkTime = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    var simulatedSourceChecks = 0;
    var simulatedWaits = 0;
    var simulatedNetworkProgress = new List<NetworkWaitProgress>();
    var restoredNetwork = await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        @"\\nas-test\kaynak",
        @"\\nas-test\hedef",
        TimeSpan.FromSeconds(30),
        new InlineProgress<NetworkWaitProgress>(simulatedNetworkProgress.Add),
        waitAsync: (delay, token) =>
        {
            token.ThrowIfCancellationRequested();
            simulatedWaits++;
            simulatedNetworkTime += delay;
            return Task.CompletedTask;
        },
        sourceAvailable: _ => ++simulatedSourceChecks >= 3,
        destinationAvailable: _ => true,
        utcNow: () => simulatedNetworkTime);
    Assert(restoredNetwork
           && simulatedSourceChecks == 3
           && simulatedWaits == 2
           && simulatedNetworkProgress.Count == 2
           && simulatedNetworkProgress.All(progress =>
               progress.UnavailablePaths?.SequenceEqual([@"\\nas-test\kaynak"]) == true),
        "NAS kaynağı iki yoklamadan sonra geri geldiğinde bekleme akışı transferi sürdürmeli");
    var simulatedDestinationTime = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    var simulatedDestinationChecks = 0;
    var immediateRetryWaits = 0;
    var restoredDestination = await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        @"\\nas-test\kaynak",
        @"\\nas-test\hedef",
        TimeSpan.FromSeconds(30),
        waitAsync: (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            immediateRetryWaits++;
            return Task.CompletedTask;
        },
        sourceAvailable: _ => true,
        destinationAvailable: _ => ++simulatedDestinationChecks >= 2,
        utcNow: () => simulatedDestinationTime);
    Assert(restoredDestination && simulatedDestinationChecks == 2 && immediateRetryWaits == 1,
        "Şimdi yeniden dene sinyali gibi erken uyandırılan NAS hedefi beklemeden tekrar yoklanmalı");
    var simulatedTimeoutTime = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    var simulatedTimeoutProgress = new List<NetworkWaitProgress>();
    var timedOutNetwork = await new NetworkAvailabilityService().WaitForAvailabilityAsync(
        @"\\nas-test\kaynak",
        @"\\nas-test\hedef",
        TimeSpan.FromSeconds(10),
        new InlineProgress<NetworkWaitProgress>(simulatedTimeoutProgress.Add),
        waitAsync: (delay, token) =>
        {
            token.ThrowIfCancellationRequested();
            simulatedTimeoutTime += delay;
            return Task.CompletedTask;
        },
        sourceAvailable: _ => false,
        destinationAvailable: _ => true,
        utcNow: () => simulatedTimeoutTime);
    Assert(!timedOutNetwork
           && simulatedTimeoutProgress.Count == 3
           && simulatedTimeoutProgress[^1].Remaining == TimeSpan.Zero,
        "NAS bağlantısı dönmezse simüle edilen süre sonunda bekleme güvenli biçimde sona ermeli");

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

    var deletionHistoryStore = new DeletionHistoryStore(storeDirectory);
    var deletionHistoryEntry = new DeletionHistoryEntry(
        Guid.NewGuid(), DateTimeOffset.UtcNow, DeletionMode.Permanent, 2, 1, 1, 4097, false);
    await deletionHistoryStore.AddAsync(deletionHistoryEntry);
    var deletionHistory = await deletionHistoryStore.LoadAsync();
    var deletionHistoryJson = await File.ReadAllTextAsync(Path.Combine(storeDirectory, "deletion-history.json"));
    Assert(deletionHistory is [{ RequestedTargetCount: 2, SucceededCount: 1, FailedCount: 1 }]
           && !deletionHistoryJson.Contains(tempSource, StringComparison.OrdinalIgnoreCase),
        "Silme geçmişi yalnızca gizlilik koruyan sayısal özeti saklamalı, hedef yollarını yazmamalı");
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
    if (Directory.Exists(deletionTestRoot))
        Directory.Delete(deletionTestRoot, true);
    if (Directory.Exists(portableDetectionRoot))
        Directory.Delete(portableDetectionRoot, true);
}

// Telefondan aktar: sahte aygıtla motor kuralları, gerçek kabuk (COM) arka ucuyla yerel geçici klasör.
var phoneTestRoot = Path.Combine(Path.GetTempPath(), "CopyPastePhoneTests", Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(phoneTestRoot);
    var phoneOptions = new PhoneImportOptions { ProgressInterval = TimeSpan.Zero };
    var photoModified = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
    var photoCreated = new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero);
    var heicBytes = RandomNumberGenerator.GetBytes(300_000);
    var jpgBytes = RandomNumberGenerator.GetBytes(4_000);
    var movBytes = RandomNumberGenerator.GetBytes(1_500_000);
    FakeDeviceNode BuildIphone() => FakeDeviceNode.Folder("Apple iPhone",
        FakeDeviceNode.Folder("Internal Storage",
            FakeDeviceNode.Folder("DCIM",
                FakeDeviceNode.Folder("100APPLE",
                    FakeDeviceNode.File("IMG_0001.HEIC", heicBytes, photoModified, photoCreated),
                    FakeDeviceNode.File("IMG_0002.JPG", jpgBytes)),
                FakeDeviceNode.Folder("101APPLE",
                    FakeDeviceNode.File("IMG_0101.MOV", movBytes)))));
    string[] PhoneLeftovers(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Contains(PhoneImportEngine.TemporaryMarker, StringComparison.Ordinal))
            .ToArray()
        : [];
    async Task<(FakePortableDeviceSource Source, PortableDeviceEntry Dcim)> OpenFakeAsync(FakeDeviceNode root)
    {
        var fake = new FakePortableDeviceSource("Fake", root);
        var catalog = new PortableDeviceCatalog([fake]);
        var discovery = await catalog.DiscoverAsync(CancellationToken.None);
        var opened = await catalog.OpenAsync(discovery.Devices[0], discovery, CancellationToken.None);
        return (fake, opened.Path[^1]);
    }

    // 1) Alt klasör yapısı, içerik, tarih, ilerleme.
    var (fakeSource, fakeDcim) = await OpenFakeAsync(BuildIphone());
    var phoneEngine = new PhoneImportEngine(phoneOptions);
    var structureDestination = Path.Combine(phoneTestRoot, "hedef-yapı");
    var structurePlan = await phoneEngine.PlanAsync(fakeSource, fakeDcim, "Apple iPhone › DCIM", structureDestination,
        null, CancellationToken.None);
    var phoneProgress = new List<PhoneImportProgress>();
    var structureResult = await phoneEngine.ImportAsync(structurePlan,
        new InlineProgress<PhoneImportProgress>(phoneProgress.Add), CancellationToken.None);
    var importedHeic = Path.Combine(structureDestination, "100APPLE", "IMG_0001.HEIC");
    Assert(fakeDcim.Name == "DCIM" && structurePlan.FileCount == 3 && structurePlan.ExistingCount == 0
           && structurePlan.ReportedTotalBytes == heicBytes.Length + jpgBytes.Length + movBytes.Length
           && structureResult is { Status: PhoneImportStatus.Completed, CopiedCount: 3, SkippedExistingCount: 0, FailedCount: 0, NotProcessedCount: 0 }
           && File.ReadAllBytes(importedHeic).AsSpan().SequenceEqual(heicBytes)
           && File.ReadAllBytes(Path.Combine(structureDestination, "100APPLE", "IMG_0002.JPG")).AsSpan().SequenceEqual(jpgBytes)
           && File.ReadAllBytes(Path.Combine(structureDestination, "101APPLE", "IMG_0101.MOV")).AsSpan().SequenceEqual(movBytes)
           && PhoneLeftovers(structureDestination).Length == 0,
        "Telefondan aktar: DCIM alt klasör yapısı korunarak birebir kopyalanmalı ve geçici dosya kalmamalı");
    Assert(File.GetLastWriteTimeUtc(importedHeic) == photoModified.UtcDateTime
           && File.GetCreationTimeUtc(importedHeic) == photoCreated.UtcDateTime,
        "Telefondan aktar: hedef dosyanın değiştirilme/oluşturma zamanı aygıtın bildirdiği tarih olmalı");
    var lastPhoneProgress = phoneProgress.LastOrDefault();
    Assert(lastPhoneProgress is { ProcessedFiles: 3, TotalFiles: 3, CopiedFiles: 3, SkippedFiles: 0, FailedFiles: 0 }
           && lastPhoneProgress.ProcessedBytes == lastPhoneProgress.TotalBytes
           && lastPhoneProgress.CopiedBytes == structurePlan.ReportedTotalBytes
           && phoneProgress.Any(value => value.CurrentItem?.EndsWith("IMG_0101.MOV", StringComparison.Ordinal) == true
                                         && value.CopiedBytes > 0)
           && phoneProgress.Zip(phoneProgress.Skip(1)).All(pair => pair.Second.ProcessedBytes >= pair.First.ProcessedBytes),
        "Telefondan aktar: ilerleme dosya n/N, bayt ve geçerli dosya adını tutarlı bildirmeli");

    // 2) Var olan dosya atlanır, içeriği değişmez; yeniden çalıştırmak kaldığı yerden devam eder.
    var skipDestination = Path.Combine(phoneTestRoot, "hedef-atla");
    Directory.CreateDirectory(Path.Combine(skipDestination, "100APPLE"));
    var existingPhonePath = Path.Combine(skipDestination, "100APPLE", "IMG_0002.JPG");
    await File.WriteAllTextAsync(existingPhonePath, "ESKİ İÇERİK");
    var skipPlan = await phoneEngine.PlanAsync(fakeSource, fakeDcim, "DCIM", skipDestination, null, CancellationToken.None);
    var skipResult = await phoneEngine.ImportAsync(skipPlan, null, CancellationToken.None);
    var rerunResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(fakeSource, fakeDcim, "DCIM", structureDestination, null, CancellationToken.None),
        null, CancellationToken.None);
    Assert(skipPlan.ExistingCount == 1 && skipPlan.ReportedBytesToCopy == heicBytes.Length + movBytes.Length
           && skipResult is { Status: PhoneImportStatus.Completed, CopiedCount: 2, SkippedExistingCount: 1 }
           && await File.ReadAllTextAsync(existingPhonePath) == "ESKİ İÇERİK"
           && rerunResult is { CopiedCount: 0, SkippedExistingCount: 3, FailedCount: 0 }
           && File.ReadAllBytes(importedHeic).AsSpan().SequenceEqual(heicBytes),
        "Telefondan aktar: hedefte aynı adlı dosya üzerine yazılmadan atlanmalı, yeniden çalıştırma yalnız eksikleri almalı");

    // 3) Yarıda kesilen akış (0x8007001F) sonrası ne son ad ne geçici dosya kalır; sonraki dosyalar işlenir.
    var brokenTree = BuildIphone();
    brokenTree.Find("IMG_0001.HEIC")!.FailAfterBytes = 70_000;
    brokenTree.Find("IMG_0001.HEIC")!.ReadError = new COMException(
        "A device attached to the system is not functioning.", PhoneImportErrors.DeviceNotFunctioningHResult);
    var (brokenSource, brokenDcim) = await OpenFakeAsync(brokenTree);
    var brokenDestination = Path.Combine(phoneTestRoot, "hedef-kesik");
    var brokenResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(brokenSource, brokenDcim, "DCIM", brokenDestination, null, CancellationToken.None),
        null, CancellationToken.None);
    Assert(brokenResult is { Status: PhoneImportStatus.CompletedWithErrors, CopiedCount: 2, FailedCount: 1, NotProcessedCount: 0, SuggestKeepOriginals: true }
           && brokenResult.Failures is [{ Kind: PhoneImportErrorKind.DeviceNotFunctioning }]
           && brokenResult.Failures[0].RelativePath == Path.Combine("100APPLE", "IMG_0001.HEIC")
           && !File.Exists(Path.Combine(brokenDestination, "100APPLE", "IMG_0001.HEIC"))
           && File.Exists(Path.Combine(brokenDestination, "101APPLE", "IMG_0101.MOV"))
           && PhoneLeftovers(brokenDestination).Length == 0,
        "Telefondan aktar: yarıda kesilen dosya hedefte yarım ya da geçici bırakılmamalı, öğe hatasından sonra devam edilmeli");
    Assert(PhoneImportErrors.KeepOriginalsHint(english: false).Contains("Orijinalleri Koru", StringComparison.Ordinal)
           && PhoneImportErrors.KeepOriginalsHint(english: true).Contains("Keep Originals", StringComparison.Ordinal)
           && PhoneImportErrors.Classify(new COMException("x", unchecked((int)0x8007001F))) == PhoneImportErrorKind.DeviceNotFunctioning
           && !PhoneImportErrors.StopsImport(PhoneImportErrorKind.DeviceNotFunctioning),
        "Telefondan aktar: 0x8007001F 'Orijinalleri Koru' ipucuyla eşlenmeli ve aktarmayı durdurmamalı");

    // 4) İptal: geçerli dosya silinir, kalanlar işlenmedi sayılır.
    var cancelTree = BuildIphone();
    using var phoneCancellation = new CancellationTokenSource();
    cancelTree.Find("IMG_0002.JPG")!.OnRead = _ => phoneCancellation.Cancel();
    var (cancelSource, cancelDcim) = await OpenFakeAsync(cancelTree);
    var cancelDestination = Path.Combine(phoneTestRoot, "hedef-iptal");
    var cancelResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(cancelSource, cancelDcim, "DCIM", cancelDestination, null, CancellationToken.None),
        null, phoneCancellation.Token);
    Assert(cancelResult is { Status: PhoneImportStatus.Cancelled, CopiedCount: 1, FailedCount: 0, NotProcessedCount: 2 }
           && File.Exists(Path.Combine(cancelDestination, "100APPLE", "IMG_0001.HEIC"))
           && !File.Exists(Path.Combine(cancelDestination, "100APPLE", "IMG_0002.JPG"))
           && !File.Exists(Path.Combine(cancelDestination, "101APPLE", "IMG_0101.MOV"))
           && PhoneLeftovers(cancelDestination).Length == 0,
        "Telefondan aktar: iptalde yarım dosya ve geçici dosya kalmamalı, kalanlar işlenmedi sayılmalı");

    // 5) Aygıt koptu: dur, kalanları işlenmedi say. Aygıt yanıt veriyorsa aynı kod yalnız o öğeyi düşürür.
    var lostTree = BuildIphone();
    var (lostSource, lostDcim) = await OpenFakeAsync(lostTree);
    lostTree.Find("IMG_0002.JPG")!.OnOpen = () =>
    {
        lostSource.Disconnected = true;
        throw new COMException("The device is not connected.", unchecked((int)0x8007048F));
    };
    var lostDestination = Path.Combine(phoneTestRoot, "hedef-koptu");
    var lostResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(lostSource, lostDcim, "DCIM", lostDestination, null, CancellationToken.None),
        null, CancellationToken.None);
    Assert(lostResult is { Status: PhoneImportStatus.StoppedDeviceDisconnected, CopiedCount: 1, FailedCount: 1, NotProcessedCount: 1 }
           && lostResult.Failures is [{ Kind: PhoneImportErrorKind.DeviceDisconnected }]
           && lostResult.Failures[0].HResult == unchecked((int)0x8007048F)
           && !File.Exists(Path.Combine(lostDestination, "101APPLE", "IMG_0101.MOV"))
           && PhoneLeftovers(lostDestination).Length == 0,
        "Telefondan aktar: aygıt kopunca durmalı ve kalan dosyaları işlenmedi olarak bildirmeli");
    var flakyTree = BuildIphone();
    flakyTree.Find("IMG_0002.JPG")!.OnOpen = () => throw new COMException("Element not found.", unchecked((int)0x80070490));
    var (flakySource, flakyDcim) = await OpenFakeAsync(flakyTree);
    var flakyResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(flakySource, flakyDcim, "DCIM", Path.Combine(phoneTestRoot, "hedef-tek-hata"), null,
            CancellationToken.None),
        null, CancellationToken.None);
    Assert(flakyResult is { Status: PhoneImportStatus.CompletedWithErrors, CopiedCount: 2, FailedCount: 1, NotProcessedCount: 0 },
        "Telefondan aktar: kopma koduna rağmen aygıt yanıt veriyorsa yalnız o öğe başarısız sayılıp devam edilmeli");

    // 6) Yanıt vermeyen okuma zaman aşımıyla bırakılır, aktarma kilitlenmez.
    var stallTree = BuildIphone();
    stallTree.Find("IMG_0002.JPG")!.HangOnRead = true;
    var (stallSource, stallDcim) = await OpenFakeAsync(stallTree);
    var stallEngine = new PhoneImportEngine(new PhoneImportOptions
    {
        StallTimeout = TimeSpan.FromMilliseconds(300),
        ProbeTimeout = TimeSpan.FromSeconds(2)
    });
    var stallDestination = Path.Combine(phoneTestRoot, "hedef-takılma");
    var stallResult = await stallEngine.ImportAsync(
        await stallEngine.PlanAsync(stallSource, stallDcim, "DCIM", stallDestination, null, CancellationToken.None),
        null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
    Assert(stallResult is { Status: PhoneImportStatus.CompletedWithErrors, CopiedCount: 2, FailedCount: 1 }
           && File.ReadAllBytes(Path.Combine(stallDestination, "101APPLE", "IMG_0101.MOV")).AsSpan().SequenceEqual(movBytes)
           && PhoneLeftovers(stallDestination).Length == 0,
        "Telefondan aktar: takılan okuma zaman aşımıyla bırakılmalı, sonraki dosya yeni tamponla doğru kopyalanmalı");

    // 7) Geçersiz karakterler, ayrılmış adlar, büyük/küçük harf çakışması ve uzun ad.
    var longPhoneName = new string('u', 300) + ".jpg";
    var namesTree = FakeDeviceNode.Folder("Phone", FakeDeviceNode.Folder("Storage", FakeDeviceNode.Folder("DCIM",
        FakeDeviceNode.File("a:b?.jpg", [1]),
        FakeDeviceNode.File("CON.jpg", [2]),
        FakeDeviceNode.File("nokta.", [3]),
        FakeDeviceNode.File("IMG.JPG", [4]),
        FakeDeviceNode.File("img.jpg", [5]),
        FakeDeviceNode.File("a_b_.jpg", [6]),
        FakeDeviceNode.File(longPhoneName, [7]))));
    var (namesSource, namesDcim) = await OpenFakeAsync(namesTree);
    var namesDestination = Path.Combine(phoneTestRoot, "hedef-adlar");
    var namesPlan = await phoneEngine.PlanAsync(namesSource, namesDcim, "DCIM", namesDestination, null, CancellationToken.None);
    var namesResult = await phoneEngine.ImportAsync(namesPlan, null, CancellationToken.None);
    var importedNames = Directory.Exists(namesDestination)
        ? Directory.EnumerateFiles(namesDestination).Select(path => Path.GetFileName(path)!)
            .ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(namesDestination, name)).Single(),
                StringComparer.Ordinal)
        : new Dictionary<string, byte>();
    var truncatedPhoneName = new string('u', 251) + ".jpg";
    Assert(namesResult is { Status: PhoneImportStatus.Completed, CopiedCount: 7 }
           && namesPlan.RenamedCount == 5
           && importedNames.GetValueOrDefault("a_b_.jpg") == 6
           && importedNames.GetValueOrDefault("a_b_ (2).jpg") == 1
           && importedNames.GetValueOrDefault("_CON.jpg") == 2
           && importedNames.GetValueOrDefault("nokta") == 3
           && importedNames.GetValueOrDefault("IMG.JPG") == 4
           && importedNames.GetValueOrDefault("img (2).jpg") == 5
           && importedNames.GetValueOrDefault(truncatedPhoneName) == 7,
        "Telefondan aktar: NTFS'te geçersiz, ayrılmış, çakışan ve uzun adlar kayıpsız ve kararlı biçimde güvenli adlara çevrilmeli ("
        + string.Join(", ", importedNames.Keys.Select(name => name.Length > 40 ? name[..12] + "…(" + name.Length + ")" : name)) + ")");
    Assert(PhoneImportPathSanitizer.SanitizeName("a<b>c|d\"e*f") == "a_b_c_d_e_f"
           && PhoneImportPathSanitizer.SanitizeName("..") == "_"
           && PhoneImportPathSanitizer.SanitizeName("lpt1 .txt") == "_lpt1 .txt"
           && PhoneImportPathSanitizer.SanitizeName("Çiçek ğüşı.heic") == "Çiçek ğüşı.heic",
        "Telefondan aktar: ad temizleyici geçersiz karakterleri, nokta adlarını ve ayrılmış aygıt adlarını ele almalı");

    // 8) Doğrulama: geçici dosya okunan veriyle eşleşmezse silinir; son ad bu arada oluşursa dokunulmaz.
    var verifyEngine = new PhoneImportEngine(new PhoneImportOptions
    {
        AfterTemporaryFileWritten = (temporary, final) =>
        {
            if (final.EndsWith("IMG_0001.HEIC", StringComparison.Ordinal))
                File.WriteAllBytes(temporary, [9, 9, 9]);
            else if (final.EndsWith("IMG_0002.JPG", StringComparison.Ordinal))
                File.WriteAllText(final, "YARIŞ");
        }
    });
    var verifyDestination = Path.Combine(phoneTestRoot, "hedef-doğrulama");
    var verifyResult = await verifyEngine.ImportAsync(
        await verifyEngine.PlanAsync(fakeSource, fakeDcim, "DCIM", verifyDestination, null, CancellationToken.None),
        null, CancellationToken.None);
    Assert(verifyResult is { CopiedCount: 1, SkippedExistingCount: 1, FailedCount: 1 }
           && verifyResult.Failures is [{ Kind: PhoneImportErrorKind.VerificationFailed }]
           && !File.Exists(Path.Combine(verifyDestination, "100APPLE", "IMG_0001.HEIC"))
           && File.ReadAllText(Path.Combine(verifyDestination, "100APPLE", "IMG_0002.JPG")) == "YARIŞ"
           && PhoneLeftovers(verifyDestination).Length == 0,
        "Telefondan aktar: SHA-256 eşleşmezse dosya silinmeli, aradaki sürede oluşan hedef dosyanın üzerine yazılmamalı");

    // 9) Bildirilen boyut ≠ okunan bayt (iPhone dönüştürmesi): başarısızlık değil, not.
    var convertedTree = BuildIphone();
    convertedTree.Find("IMG_0002.JPG")!.ReportedSize = 10;
    var (convertedSource, convertedDcim) = await OpenFakeAsync(convertedTree);
    var convertedResult = await phoneEngine.ImportAsync(
        await phoneEngine.PlanAsync(convertedSource, convertedDcim, "DCIM", Path.Combine(phoneTestRoot, "hedef-boyut"), null,
            CancellationToken.None),
        null, CancellationToken.None);
    Assert(convertedResult is { Status: PhoneImportStatus.Completed, CopiedCount: 3, SizeMismatchCount: 1 }
           && convertedResult.SizeMismatches.SequenceEqual([Path.Combine("100APPLE", "IMG_0002.JPG")]),
        "Telefondan aktar: bildirilen boyuttan farklı bayt sayısı başarısızlık sayılmamalı, sonuçta not edilmeli");

    // 10) Hata sınıflandırması ve hedef doğrulaması.
    Assert(new[] { 0x8007048F, 0x80070015, 0x80070079, 0x800701B1, 0x80070490 }
               .All(code => PhoneImportErrors.ClassifyHResult(unchecked((int)code)) == PhoneImportErrorKind.DeviceDisconnected)
           && PhoneImportErrors.Classify(new IOException("dolu", unchecked((int)0x80070070))) == PhoneImportErrorKind.DestinationFull
           && PhoneImportErrors.StopsImport(PhoneImportErrorKind.DestinationFull)
           && PhoneImportErrors.StopsImport(PhoneImportErrorKind.DeviceDisconnected)
           && PhoneImportErrors.Classify(new UnauthorizedAccessException()) == PhoneImportErrorKind.AccessDenied
           && PhoneImportErrors.Classify(new PhoneImportStallException("x")) == PhoneImportErrorKind.DeviceDisconnected,
        "Telefondan aktar: aygıt kopması, hedef dolu ve erişim hataları doğru sınıflandırılmalı");
    AssertThrows<ArgumentException>(() => PhoneImportEngine.NormalizeDestination(@"This PC\Apple iPhone\Internal Storage"),
        "Telefondan aktar: telefon yolu hedef olarak reddedilmeli");
    AssertThrows<ArgumentException>(() => PhoneImportEngine.NormalizeDestination(@"Foto\Tatil"),
        "Telefondan aktar: göreli hedef yolu reddedilmeli");
    Assert(structurePlan.FreeBytes is > 0 && structurePlan.HasEnoughSpace
           && !(structurePlan with { FreeBytes = 1 }).HasEnoughSpace,
        "Telefondan aktar: özet hedefteki boş alanı göstermeli ve yetmeyen alanı ayırt etmeli");

    // 11) Arka uç seçimi ve yedek yol.
    var failingPrimary = new FakePortableDeviceSource("WinRT", BuildIphone())
    {
        DevicesError = new COMException("broken", unchecked((int)0x80070005))
    };
    var workingFallback = new FakePortableDeviceSource("Shell", BuildIphone());
    var failingDiscovery = await new PortableDeviceCatalog([failingPrimary, workingFallback])
        .DiscoverAsync(CancellationToken.None);
    Assert(failingDiscovery.Devices is [{ BackendName: "Shell" }]
           && failingDiscovery.Reports.Count == 2
           && failingDiscovery.Reports[0] is { BackendName: "WinRT", ErrorHResult: unchecked((int)0x80070005) }
           && failingDiscovery.Reports[1] is { BackendName: "Shell", ErrorHResult: null, Devices.Count: 1 },
        "Telefondan aktar: birincil arka uç hata verirse aygıt listesi yedek arka uçtan gelmeli");
    var lockedPrimary = new FakePortableDeviceSource("WinRT", FakeDeviceNode.Folder("Apple iPhone",
        FakeDeviceNode.Folder("Internal Storage")));
    var catalogWithFallback = new PortableDeviceCatalog([lockedPrimary, workingFallback]);
    var lockedDiscovery = await catalogWithFallback.DiscoverAsync(CancellationToken.None);
    var fallbackOpen = await catalogWithFallback.OpenAsync(lockedDiscovery.Devices[0], lockedDiscovery,
        CancellationToken.None);
    Assert(lockedDiscovery.Devices is [{ BackendName: "WinRT" }]
           && fallbackOpen is { Outcome: PortableDeviceOpenOutcome.DcimFound, Device.BackendName: "Shell" }
           && fallbackOpen.Path.Select(entry => entry.Name).SequenceEqual(["Apple iPhone", "Internal Storage", "DCIM"])
           && fallbackOpen.Attempts.Count == 2,
        "Telefondan aktar: birincil yol DCIM'i göremezse aynı aygıt yedek yoldan açılmalı");
    var lockedOnly = new PortableDeviceCatalog([lockedPrimary]);
    var lockedOnlyDiscovery = await lockedOnly.DiscoverAsync(CancellationToken.None);
    var lockedOpen = await lockedOnly.OpenAsync(lockedOnlyDiscovery.Devices[0], lockedOnlyDiscovery, CancellationToken.None);
    var unopenedPrimary = lockedDiscovery.Devices[0] with { Root = null, ErrorHResult = unchecked((int)0x80070005) };
    var unopenedOpen = await catalogWithFallback.OpenAsync(unopenedPrimary, lockedDiscovery, CancellationToken.None);
    Assert(lockedOpen.Outcome == PortableDeviceOpenOutcome.StorageEmpty
           && unopenedOpen is { Outcome: PortableDeviceOpenOutcome.DcimFound, Device.BackendName: "Shell" },
        "Telefondan aktar: boş depolama kilit/güven durumu olarak ayırt edilmeli, açılamayan aygıt yedek yoldan denenmeli");

    // 12) Tanılama metni.
    var phoneDiagnostics = new PhoneImportDiagnostics
    {
        AppVersion = "1.7.6.0",
        Discovery = failingDiscovery,
        LastOpen = fallbackOpen,
        LastPlan = structurePlan,
        LastResult = brokenResult
    };
    phoneDiagnostics.RecordError("WinRT aygıt listesi", unchecked((int)0x80070005), "Access is denied.");
    var turkishDiagnostics = phoneDiagnostics.Build(english: false);
    var englishDiagnostics = phoneDiagnostics.Build(english: true);
    Assert(turkishDiagnostics.Contains("Telefondan aktar tanılaması", StringComparison.Ordinal)
           && turkishDiagnostics.Contains("[WinRT] HATA 0x80070005", StringComparison.Ordinal)
           && turkishDiagnostics.Contains("[Shell] 1 aygıt", StringComparison.Ordinal)
           && turkishDiagnostics.Contains("0x8007001F DeviceNotFunctioning", StringComparison.Ordinal)
           && turkishDiagnostics.Contains("DCIM bulundu", StringComparison.Ordinal)
           && englishDiagnostics.Contains("Import from phone diagnostics", StringComparison.Ordinal)
           && englishDiagnostics.Contains("DCIM found", StringComparison.Ordinal),
        "Telefondan aktar: tanılama metni arka uçları, aygıtları, DCIM durumunu ve HRESULT'ları iki dilde içermeli");

    // 13) Gerçek kabuk (COM) arka ucu yerel geçici klasörde: ad (uzantı gizlemeden bağımsız), boyut, tarih, okuma, iç içe klasör.
    var shellSourceRoot = Path.Combine(phoneTestRoot, "kabuk-kaynak");
    var shellNested = Path.Combine(shellSourceRoot, "alt", "iç içe");
    Directory.CreateDirectory(shellNested);
    var shellPhoto = RandomNumberGenerator.GetBytes(150_000);
    var shellVideo = RandomNumberGenerator.GetBytes(2_600_000);
    var shellUnicode = RandomNumberGenerator.GetBytes(1_234);
    var shellPhotoPath = Path.Combine(shellSourceRoot, "foto.jpg");
    await File.WriteAllBytesAsync(shellPhotoPath, shellPhoto);
    await File.WriteAllBytesAsync(Path.Combine(shellNested, "video.mov"), shellVideo);
    await File.WriteAllBytesAsync(Path.Combine(shellSourceRoot, "Çiçek ğüşı.heic"), shellUnicode);
    var shellPhotoTime = new DateTime(2023, 3, 4, 5, 6, 7, DateTimeKind.Utc);
    File.SetLastWriteTimeUtc(shellPhotoPath, shellPhotoTime);
    var shellZipFolder = Path.Combine(phoneTestRoot, "zip-içerik");
    Directory.CreateDirectory(shellZipFolder);
    await File.WriteAllTextAsync(Path.Combine(shellZipFolder, "not.txt"), "zip");
    System.IO.Compression.ZipFile.CreateFromDirectory(shellZipFolder, Path.Combine(shellSourceRoot, "arşiv.zip"));
    var hideExtensions = Microsoft.Win32.Registry.GetValue(
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", null);
    using (var shellSource = new ShellPortableDeviceSource())
    {
        var shellDevices = await shellSource.GetDevicesAsync(CancellationToken.None);
        Console.WriteLine($"Telefon testi: kabuk 'Bu bilgisayar' altında {shellDevices.Count} taşınabilir aygıt gördü; "
            + $"Explorer HideFileExt={hideExtensions ?? "(yok)"}.");
        var shellFolder = await shellSource.GetFolderAsync(shellSourceRoot, CancellationToken.None);
        var shellChildren = await shellSource.GetChildrenAsync(shellFolder, CancellationToken.None);
        var shellByName = shellChildren.ToDictionary(child => child.Name, StringComparer.Ordinal);
        Assert(shellByName.Count == 4
               && shellByName.TryGetValue("foto.jpg", out var shellPhotoEntry)
               && shellPhotoEntry is { IsFolder: false, ReportedSize: 150_000 }
               && shellPhotoEntry.Modified?.UtcDateTime == shellPhotoTime
               && shellByName.TryGetValue("Çiçek ğüşı.heic", out var shellUnicodeEntry)
               && shellUnicodeEntry.ReportedSize == shellUnicode.Length
               && shellByName.TryGetValue("alt", out var shellAltEntry) && shellAltEntry.IsFolder
               && shellByName.TryGetValue("arşiv.zip", out var shellZipEntry) && !shellZipEntry.IsFolder,
            "Gerçek kabuk arka ucu: adlar uzantılarıyla, boyut ve tarih doğru okunmalı; zip dosya sayılmalı (bulunan: "
            + string.Join(", ", shellChildren.Select(child =>
                $"{child.Name}|{(child.IsFolder ? "klasör" : child.ReportedSize)}|{child.Modified:O}")) + ")");
        if (shellByName.TryGetValue("foto.jpg", out var readEntry))
        {
            await using var shellStream = await shellSource.OpenReadAsync(readEntry, CancellationToken.None);
            using var shellCopy = new MemoryStream();
            await shellStream.CopyToAsync(shellCopy);
            Assert(shellCopy.ToArray().AsSpan().SequenceEqual(shellPhoto),
                "Gerçek kabuk arka ucu: IStream ile okunan baytlar dosyanın kendisiyle aynı olmalı");
        }
        var shellDestination = Path.Combine(phoneTestRoot, "kabuk-hedef");
        var shellEngine = new PhoneImportEngine(new PhoneImportOptions { BufferSize = 256 * 1024 });
        var shellPlan = await shellEngine.PlanAsync(shellSource, shellFolder, shellSourceRoot, shellDestination, null,
            CancellationToken.None);
        var shellResult = await shellEngine.ImportAsync(shellPlan, null, CancellationToken.None);
        Assert(shellPlan.FileCount == 4
               && shellResult is { Status: PhoneImportStatus.Completed, CopiedCount: 4, BackendName: ShellPortableDeviceSource.Name }
               && File.ReadAllBytes(Path.Combine(shellDestination, "alt", "iç içe", "video.mov")).AsSpan().SequenceEqual(shellVideo)
               && File.ReadAllBytes(Path.Combine(shellDestination, "Çiçek ğüşı.heic")).AsSpan().SequenceEqual(shellUnicode)
               && File.GetLastWriteTimeUtc(Path.Combine(shellDestination, "foto.jpg")) == shellPhotoTime
               && File.Exists(Path.Combine(shellDestination, "arşiv.zip"))
               && PhoneLeftovers(shellDestination).Length == 0,
            "Gerçek kabuk arka ucu: iç içe klasörler, Unicode adlar ve tarihlerle uçtan uca içe aktarma doğru olmalı");
        Console.WriteLine($"Telefon testi: kabuk arka ucuyla {shellResult.CopiedCount} dosya, "
            + $"{PhoneImportDiagnostics.FormatBytes(shellResult.CopiedBytes)} aktarıldı ({shellResult.Elapsed.TotalMilliseconds:N0} ms).");

        // .url kısayollarının uzantısı (NeverShowExt) kullanıcı ayarından bağımsız olarak görünen adda hep gizlenir.
        var shellNamesRoot = Path.Combine(phoneTestRoot, "kabuk-adlar");
        Directory.CreateDirectory(shellNamesRoot);
        var shortcutPath = Path.Combine(shellNamesRoot, "kısayol.url");
        await File.WriteAllTextAsync(shortcutPath, "[InternetShortcut]\r\nURL=https://example.com/\r\n");
        var shortcutDisplayName = (await shellSource.GetFolderAsync(shortcutPath, CancellationToken.None)).Name;
        var shellNameChildren = await shellSource.GetChildrenAsync(
            await shellSource.GetFolderAsync(shellNamesRoot, CancellationToken.None), CancellationToken.None);
        Console.WriteLine($"Telefon testi: '.url' görünen ad (SIGDN_NORMALDISPLAY) '{shortcutDisplayName}', "
            + $"arka ucun dosya adı '{string.Join(", ", shellNameChildren.Select(child => child.Name))}'.");
        Assert(shellNameChildren is [{ Name: "kısayol.url", IsFolder: false }],
            "Gerçek kabuk arka ucu: uzantısı her zaman gizlenen dosyada bile ad uzantısıyla (PKEY_FileName) okunmalı");
    }

    // 14) Tanılamada seri numarası maskesi: VID/PID ve arabirim sınıfı kalır, benzersiz bölüm "…" olur.
    const string iphoneSerial = "00008110001E2D3A0E88801E";
    var maskCases = new (string? Input, string Expected)[]
    {
        ($@"\\?\usb#vid_05ac&pid_12a8#{iphoneSerial}#{{6ac27878-a6fa-4155-ba85-f98f491d4f33}}",
            @"\\?\usb#vid_05ac&pid_12a8#…#{6ac27878-a6fa-4155-ba85-f98f491d4f33}"),
        ($@"\\?\USB#VID_05AC&PID_12A8#{iphoneSerial}#{{6AC27878-A6FA-4155-BA85-F98F491D4F33}}",
            @"\\?\USB#VID_05AC&PID_12A8#…#{6AC27878-A6FA-4155-BA85-F98F491D4F33}"),
        ($@"::{{20D04FE0-3AEA-1069-A2D8-08002B30309D}}\\\?\usb#vid_05ac&pid_12a8#{iphoneSerial}#{{6ac27878-a6fa-4155-ba85-f98f491d4f33}}\SID-{{10001,,31708622848}}\DCIM",
            @"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\?\usb#vid_05ac&pid_12a8#…#{6ac27878-a6fa-4155-ba85-f98f491d4f33}\SID-{10001,,31708622848}\DCIM"),
        (@"\\?\swd#wpdbusenum#_??_usbstor#disk&ven_samsung&prod_flash_drive&rev_1100#0376023070015479&0#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}#{6ac27878-a6fa-4155-ba85-f98f491d4f33}",
            @"\\?\swd#wpdbusenum#_??_usbstor#disk&ven_samsung&prod_flash_drive&rev_1100#…#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}#{6ac27878-a6fa-4155-ba85-f98f491d4f33}"),
        (@"\\?\SWD#WPDBUSENUM#{1F0A1B2C-3D4E-5F60-7182-93A4B5C6D7E8}#0000000000100000",
            @"\\?\SWD#WPDBUSENUM#…#…"),
        ("picker:DCIM", "picker:DCIM"),
        ("", ""),
        (null, "")
    };
    foreach (var (input, expected) in maskCases)
    {
        var masked = PhoneImportDiagnostics.MaskDeviceId(input);
        Assert(masked == expected, $"Tanılama seri maskesi '{input}' için '{expected}' olmalı, '{masked}' bulundu");
    }
    var maskedDiscovery = new PortableDeviceDiscovery(
        [],
        [new PortableDeviceBackendReport("Shell",
            [new PortableDeviceInfo("Apple iPhone",
                $@"::{{20D04FE0-3AEA-1069-A2D8-08002B30309D}}\\\?\usb#vid_05ac&pid_12a8#{iphoneSerial}#{{6ac27878-a6fa-4155-ba85-f98f491d4f33}}",
                workingFallback, null, unchecked((int)0x80070005), "denied")],
            null, null, TimeSpan.FromMilliseconds(5))]);
    var maskedText = new PhoneImportDiagnostics { Discovery = maskedDiscovery }.Build(english: false);
    Assert(!maskedText.Contains(iphoneSerial, StringComparison.OrdinalIgnoreCase)
           && maskedText.Contains("vid_05ac&pid_12a8#…#", StringComparison.Ordinal),
        "Tanılama metni aygıt seri numarasını içermemeli, VID/PID'yi göstermeli");

    // 15) Kaynak değişmezliği: gerçek kabuk arka ucuyla yerel "aygıt klasörü" tam aktarma, yeniden çalıştırma,
    // iptal ve okuma hatasından sonra yol listesi, boyut, tarihler, öznitelikler ve SHA-256 olarak birebir aynı kalmalı.
    var unchangedSource = Path.Combine(phoneTestRoot, "değişmez-kaynak");
    Directory.CreateDirectory(Path.Combine(unchangedSource, "100APPLE"));
    Directory.CreateDirectory(Path.Combine(unchangedSource, "101APPLE", "iç içe"));
    var unchangedFiles = new (string RelativePath, int Size)[]
    {
        (Path.Combine("100APPLE", "IMG_0001.HEIC"), 300_000),
        (Path.Combine("100APPLE", "Çiçek ğüşı.jpg"), 12_345),
        (Path.Combine("100APPLE", "salt-okunur.JPG"), 4_096),
        (Path.Combine("101APPLE", "iç içe", "VID_0002.MOV"), 2_500_000),
        (Path.Combine("101APPLE", "IMG_0003.AAE"), 700)
    };
    var fixedTime = new DateTime(2022, 7, 8, 9, 10, 11, DateTimeKind.Utc);
    foreach (var (relativePath, size) in unchangedFiles)
    {
        var path = Path.Combine(unchangedSource, relativePath);
        await File.WriteAllBytesAsync(path, RandomNumberGenerator.GetBytes(size));
        File.SetCreationTimeUtc(path, fixedTime.AddDays(-3));
        File.SetLastWriteTimeUtc(path, fixedTime);
    }
    var readOnlySourceFile = Path.Combine(unchangedSource, "100APPLE", "salt-okunur.JPG");
    File.SetAttributes(readOnlySourceFile, FileAttributes.ReadOnly | FileAttributes.Archive);
    var lockedSourceFile = Path.Combine(unchangedSource, "100APPLE", "Çiçek ğüşı.jpg");
    var sourceBefore = SourceSnapshot.Take(unchangedSource);
    using (var unchangedShell = new ShellPortableDeviceSource())
    {
        var unchangedFolder = await unchangedShell.GetFolderAsync(unchangedSource, CancellationToken.None);
        var unchangedEngine = new PhoneImportEngine(new PhoneImportOptions { ProgressInterval = TimeSpan.Zero, BufferSize = 64 * 1024 });
        async Task<PhoneImportResult> RunUnchangedAsync(string destinationName, IProgress<PhoneImportProgress>? progress,
            CancellationToken cancellationToken)
        {
            var plan = await unchangedEngine.PlanAsync(unchangedShell, unchangedFolder, unchangedSource,
                Path.Combine(phoneTestRoot, destinationName), null, CancellationToken.None);
            return await unchangedEngine.ImportAsync(plan, progress, cancellationToken);
        }
        string SourceDifference() => SourceSnapshot.Describe(sourceBefore, SourceSnapshot.Take(unchangedSource));

        // (a) tam aktarma
        var fullResult = await RunUnchangedAsync("değişmez-hedef", null, CancellationToken.None);
        var afterFull = SourceDifference();
        Assert(fullResult is { Status: PhoneImportStatus.Completed, CopiedCount: 5 } && afterFull.Length == 0,
            "Kaynak değişmezliği (a) tam aktarma: kaynak birebir aynı kalmalı " + afterFull);
        // (b) yeniden çalıştırma: hepsi atlanır
        var rerunUnchanged = await RunUnchangedAsync("değişmez-hedef", null, CancellationToken.None);
        var afterRerun = SourceDifference();
        Assert(rerunUnchanged is { CopiedCount: 0, SkippedExistingCount: 5 } && afterRerun.Length == 0,
            "Kaynak değişmezliği (b) yeniden çalıştırma: hepsi atlanmalı, kaynak birebir aynı kalmalı " + afterRerun);
        // (c) aktarma ortasında iptal (büyük videonun okunması sürerken)
        using var unchangedCancellation = new CancellationTokenSource();
        var cancelProgress = new InlineProgress<PhoneImportProgress>(value =>
        {
            // Diğer dört dosyadan (317 141 bayt) sonra videonun en az 128 KB'ı okunmuşken: okuma döngüsünün ortası.
            if (value.CurrentItem?.EndsWith("VID_0002.MOV", StringComparison.Ordinal) == true
                && value.CopiedBytes > 317_141 + 128 * 1024)
                unchangedCancellation.Cancel();
        });
        var cancelledUnchanged = await RunUnchangedAsync("değişmez-iptal", cancelProgress, unchangedCancellation.Token);
        var afterCancel = SourceDifference();
        Assert(cancelledUnchanged is { Status: PhoneImportStatus.Cancelled, NotProcessedCount: > 0 }
               && !File.Exists(Path.Combine(phoneTestRoot, "değişmez-iptal", "101APPLE", "iç içe", "VID_0002.MOV"))
               && PhoneLeftovers(Path.Combine(phoneTestRoot, "değişmez-iptal")).Length == 0
               && afterCancel.Length == 0,
            "Kaynak değişmezliği (c) iptal: yarım/geçici dosya kalmamalı, kaynak birebir aynı kalmalı " + afterCancel
            + $" (durum {cancelledUnchanged.Status}, işlenmeyen {cancelledUnchanged.NotProcessedCount})");
        // (d) okuma hatası: bir kaynak dosya FileShare.None ile kilitliyken; hata sonrası devam
        PhoneImportResult lockedResult;
        using (new FileStream(lockedSourceFile, FileMode.Open, FileAccess.Read, FileShare.None))
            lockedResult = await RunUnchangedAsync("değişmez-kilit", null, CancellationToken.None);
        var afterLock = SourceDifference();
        Assert(lockedResult is { Status: PhoneImportStatus.CompletedWithErrors, CopiedCount: 4, FailedCount: 1, NotProcessedCount: 0 }
               && lockedResult.Failures is [{ RelativePath: var lockedPath }] && lockedPath.EndsWith("Çiçek ğüşı.jpg", StringComparison.Ordinal)
               && PhoneLeftovers(Path.Combine(phoneTestRoot, "değişmez-kilit")).Length == 0
               && afterLock.Length == 0,
            "Kaynak değişmezliği (d) okuma hatası: o dosya başarısız sayılıp devam edilmeli, kaynak birebir aynı kalmalı " + afterLock
            + $" (durum {lockedResult.Status}, aktarılan {lockedResult.CopiedCount}, başarısız {lockedResult.FailedCount}: "
            + string.Join(", ", lockedResult.Failures.Select(failure => $"{failure.RelativePath} {PhoneImportErrors.FormatHResult(failure.HResult)}")) + ")");
        Console.WriteLine($"Telefon testi: kaynak değişmezliği {sourceBefore.Count} öğe × 4 senaryo (tam, yeniden, iptal, kilit) karşılaştırıldı; "
            + $"kilitli dosya hatası {string.Join(", ", lockedResult.Failures.Select(failure => PhoneImportErrors.FormatHResult(failure.HResult)))}.");
    }
    Assert((File.GetAttributes(readOnlySourceFile) & FileAttributes.ReadOnly) != 0,
        "Kaynak değişmezliği: salt okunur öznitelik korunmalı");
}
finally
{
    if (Directory.Exists(phoneTestRoot))
    {
        foreach (var file in Directory.EnumerateFiles(phoneTestRoot, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(phoneTestRoot, recursive: true);
    }
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

// Gerçek Geri Dönüşüm Kutusu testi yalnız --recycle-bin ile çalışır. Kutu'ya yalnız bu testin oluşturduğu tek
// küçük dosya gider ve $R/$I eşleşmesi doğrulanınca yalnız o öğe Kutu'dan temizlenir; Kutu boşaltılmaz.
if (args.Contains("--recycle-bin", StringComparer.OrdinalIgnoreCase))
{
    var realRecycleRoot = Path.Combine(Path.GetTempPath(), "CopyPasteRecycleTests", Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(realRecycleRoot);
        var realStatus = WindowsRecycleBinStatusProvider.Instance.GetStatus(realRecycleRoot);
        Console.WriteLine($"Kutu testi: durum ilke={realStatus.DisabledByPolicy?.ToString() ?? "?"}, "
            + $"NukeOnDelete={realStatus.NukeOnDelete?.ToString() ?? "?"}, "
            + $"MaxCapacity={realStatus.MaxCapacityBytes?.ToString() ?? "?"} B, klasör={realStatus.RecycleDirectory ?? "?"}");
        var realPlanner = new DeletionPlanner();
        var shellSender = new ShellRecycleBinSender();

        var smallFile = Path.Combine(realRecycleRoot, $"copypaste-kutu-testi-{Guid.NewGuid():N}.txt");
        var smallContent = "CopyPaste Geri Dönüşüm Kutusu testi " + Guid.NewGuid();
        await File.WriteAllTextAsync(smallFile, smallContent);
        var smallPlan = realPlanner.Create([smallFile], DeletionMode.RecycleBin);
        Assert(smallPlan.IsValid, "Gerçek Kutu testi: küçük geçici dosyanın Kutu planı geçerli olmalı");
        if (smallPlan.IsValid)
        {
            var recorder = new RecordingRecycleBinSender(path => shellSender.Send(path, CancellationToken.None));
            var smallResult = await new DeletionService(realPlanner, recorder).ExecuteAsync(smallPlan);
            var sent = recorder.Results.SingleOrDefault();
            Console.WriteLine($"Kutu testi: küçük dosya sonuç={sent?.Status}, hr=0x{sent?.HResult:X8}, "
                + $"PreDeleteItem bayrakları=0x{sent?.FirstPreDeleteFlags:X}, Kutu öğesi={sent?.RecycledItemPath ?? "<yok>"}");
            Assert(smallResult.Succeeded && !File.Exists(smallFile)
                   && sent is { Status: RecycleBinSendStatus.Recycled, RecycledItemPath: not null }
                   && sent.FirstPreDeleteFlags is { } flags && ShellRecycleBinSender.IsRecycleDeletionAllowed(flags),
                "Gerçek Kutu testi: küçük dosya Kutu'ya taşınmalı ve PreDeleteItem TSF_DELETE_RECYCLE_IF_POSSIBLE almalı");
            if (sent?.RecycledItemPath is { } recycledPath)
            {
                var cleanup = RecycleBinTestCleanup.RemoveOwnItem(recycledPath, smallFile, smallContent,
                    realStatus.RecycleDirectory);
                Console.WriteLine("Kutu testi: temizlik " + cleanup);
                Assert(cleanup.StartsWith("tamam", StringComparison.Ordinal),
                    "Gerçek Kutu testi: testin Kutu öğesi ($R/$I) doğrulanıp temizlenmeli: " + cleanup);
            }
        }

        // Kutu'dan büyük seyrek dosya: diskte yer kaplamaz, mantıksal boyutu birimden büyüktür.
        var oversizedLength = new DriveInfo(Path.GetPathRoot(realRecycleRoot)!).TotalSize + (1L << 30);
        var oversizedFile = Path.Combine(realRecycleRoot, "kutuya-sığmaz.bin");
        NativeFileMethods.CreateSparseFile(oversizedFile, oversizedLength);
        if (realStatus.MaxCapacityBytes is not null)
            Assert(realPlanner.Create([oversizedFile], DeletionMode.RecycleBin).Issues
                    is [{ Code: DeletionIssueCode.ExceedsRecycleBinCapacity }],
                "Gerçek Kutu testi: kayıt defterindeki kapasiteyi aşan dosya önizlemede engellenmeli");
        else
            Console.WriteLine("Kutu testi: Kutu kapasitesi okunamadı; kapasite önizleme denetimi atlandı.");

        var oversizedFolder = Path.Combine(realRecycleRoot, "kutuya-sığmayan-klasör");
        Directory.CreateDirectory(oversizedFolder);
        var oversizedFolderSmall = Path.Combine(oversizedFolder, "küçük.txt");
        var oversizedFolderLarge = Path.Combine(oversizedFolder, "büyük.bin");
        await File.WriteAllTextAsync(oversizedFolderSmall, "küçük");
        NativeFileMethods.CreateSparseFile(oversizedFolderLarge, oversizedLength);
        var folderSent = shellSender.Send(oversizedFolder, CancellationToken.None);
        Console.WriteLine($"Kutu testi: sığmayan klasör (planlayıcı atlanarak) sonuç={folderSent.Status}, "
            + $"hr=0x{folderSent.HResult:X8}, ilk bayrak=0x{folderSent.FirstPreDeleteFlags:X}");
        Assert(folderSent is { Status: RecycleBinSendStatus.NotRecyclable, RecycledItemPath: null, SomeContentRecycled: false }
               && File.Exists(oversizedFolderSmall) && File.Exists(oversizedFolderLarge),
            "Gerçek Kutu testi: Kutu'ya sığmayan klasör PreDeleteItem korumasıyla silinmeden bırakılmalı");

        // Bilinen kabuk sınırı: tek dosya sığmazsa PreDeleteItem yine 0x80 alır ve Windows dosyayı kalıcı siler.
        // Bu yüzden planlayıcının kapasite denetimi şarttır; gönderici en azından başarı iddia etmemelidir.
        var fileSent = shellSender.Send(oversizedFile, CancellationToken.None);
        var oversizedFileExists = File.Exists(oversizedFile);
        Console.WriteLine($"Kutu testi: sığmayan dosya (planlayıcı atlanarak) sonuç={fileSent.Status}, "
            + $"hr=0x{fileSent.HResult:X8}, ilk bayrak=0x{fileSent.FirstPreDeleteFlags:X}, dosya duruyor={oversizedFileExists}");
        Assert(fileSent.Status != RecycleBinSendStatus.Recycled
               && (oversizedFileExists || fileSent.Status == RecycleBinSendStatus.DeletedPermanently),
            "Gerçek Kutu testi: Kutu'ya girmeyen dosya hiçbir zaman Kutu'ya taşındı sayılmamalı");

        var leftovers = RecycleBinTestCleanup.FindRecordsUnder(realStatus.RecycleDirectory, realRecycleRoot);
        Console.WriteLine("Kutu testi: Kutu'da bu testin kökünü gösteren kayıt sayısı " + leftovers.Count
            + (leftovers.Count > 0 ? " → " + string.Join(", ", leftovers) : string.Empty));
        Assert(leftovers.Count == 0, "Gerçek Kutu testi: Kutu'da bu teste ait öğe kalmamalı");
    }
    finally
    {
        if (Directory.Exists(realRecycleRoot))
            Directory.Delete(realRecycleRoot, recursive: true);
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

void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
        failures.Add("BAŞARISIZ: " + message);
    }
    catch (TException)
    {
    }
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

// Kaynak klasörün karşılaştırılabilir görüntüsü: göreli yol, boyut, değiştirilme/oluşturma zamanı, öznitelikler, SHA-256.
// Son erişim zamanı bilerek dışarıda: salt okuma da onu güncelleyebilir.
static class SourceSnapshot
{
    public static IReadOnlyList<string> Take(string root)
    {
        var lines = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var info = new DirectoryInfo(directory);
            lines.Add($"D|{Path.GetRelativePath(root, directory)}|{info.LastWriteTimeUtc.Ticks}|{info.CreationTimeUtc.Ticks}|{info.Attributes}");
        }
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            lines.Add($"F|{Path.GetRelativePath(root, file)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{info.CreationTimeUtc.Ticks}"
                      + $"|{info.Attributes}|{Convert.ToHexString(SHA256.HashData(stream))}");
        }
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    public static string Describe(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var removed = before.Except(after).ToArray();
        var added = after.Except(before).ToArray();
        return removed.Length + added.Length == 0
            ? string.Empty
            : "[önce: " + string.Join("; ", removed.Take(4)) + " | sonra: " + string.Join("; ", added.Take(4)) + "]";
    }
}

sealed class FakeDeviceNode
{
    private FakeDeviceNode(string name, bool isFolder)
    {
        Name = name;
        IsFolder = isFolder;
    }

    public string Name { get; }
    public bool IsFolder { get; }
    public byte[] Content { get; private init; } = [];
    public long? ReportedSize { get; set; }
    public DateTimeOffset? Modified { get; private init; }
    public DateTimeOffset? Created { get; private init; }
    public List<FakeDeviceNode> Children { get; } = [];
    public long? FailAfterBytes { get; set; }
    public Exception? ReadError { get; set; }
    public Action<long>? OnRead { get; set; }
    public Action? OnOpen { get; set; }
    public bool HangOnRead { get; set; }

    public static FakeDeviceNode Folder(string name, params FakeDeviceNode[] children)
    {
        var folder = new FakeDeviceNode(name, true);
        folder.Children.AddRange(children);
        return folder;
    }

    public static FakeDeviceNode File(string name, byte[] content, DateTimeOffset? modified = null,
        DateTimeOffset? created = null) =>
        new(name, false) { Content = content, ReportedSize = content.Length, Modified = modified, Created = created };

    public FakeDeviceNode? Find(string name) => Name == name
        ? this
        : Children.Select(child => child.Find(name)).FirstOrDefault(found => found is not null);

    public PortableDeviceEntry ToEntry() => new(Name, IsFolder, IsFolder ? null : ReportedSize, Modified, Created, this);
}

// Read-only fake device: a tree in memory, small read chunks, and injectable failures.
sealed class FakePortableDeviceSource(string backendName, FakeDeviceNode root) : IPortableDeviceSource
{
    private const int DeviceNotConnected = unchecked((int)0x8007048F);

    public string BackendName => backendName;
    public bool Disconnected { get; set; }
    public Exception? DevicesError { get; init; }

    public Task<IReadOnlyList<PortableDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        if (DevicesError is not null)
            return Task.FromException<IReadOnlyList<PortableDeviceInfo>>(DevicesError);
        return Task.FromResult<IReadOnlyList<PortableDeviceInfo>>(
            [new PortableDeviceInfo(root.Name, @"\\?\usb#vid_05ac&pid_12a8#test#{6ac27878-a6fa-4155-ba85-f98f491d4f33}",
                this, root.ToEntry())]);
    }

    public Task<IReadOnlyList<PortableDeviceEntry>> GetChildrenAsync(PortableDeviceEntry folder, CancellationToken cancellationToken)
    {
        if (Disconnected)
            return Task.FromException<IReadOnlyList<PortableDeviceEntry>>(new COMException("gone", DeviceNotConnected));
        var node = (FakeDeviceNode)folder.Handle;
        return Task.FromResult<IReadOnlyList<PortableDeviceEntry>>(node.Children.Select(child => child.ToEntry()).ToArray());
    }

    public Task<Stream> OpenReadAsync(PortableDeviceEntry file, CancellationToken cancellationToken)
    {
        if (Disconnected)
            return Task.FromException<Stream>(new COMException("gone", DeviceNotConnected));
        var node = (FakeDeviceNode)file.Handle;
        try
        {
            node.OnOpen?.Invoke();
        }
        catch (Exception ex)
        {
            return Task.FromException<Stream>(ex);
        }
        return Task.FromResult<Stream>(new FakeDeviceReadStream(node));
    }
}

sealed class FakeDeviceReadStream(FakeDeviceNode node) : Stream
{
    private const int ChunkSize = 64 * 1024;
    private long _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (node.HangOnRead)
            return new ValueTask<int>(new TaskCompletionSource<int>().Task);
        try
        {
            return ValueTask.FromResult(ReadCore(buffer.Span));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    private int ReadCore(Span<byte> buffer)
    {
        node.OnRead?.Invoke(_position);
        if (node.FailAfterBytes is { } failAt && _position >= failAt)
            throw node.ReadError ?? new IOException("interrupted");
        var limit = node.FailAfterBytes is { } stopAt ? Math.Min(node.Content.Length, stopAt) : node.Content.Length;
        var count = (int)Math.Min(Math.Min(buffer.Length, ChunkSize), Math.Max(0, limit - _position));
        node.Content.AsSpan((int)_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class FakeRecycleBinStatusProvider(Func<string, RecycleBinVolumeStatus> status) : IRecycleBinStatusProvider
{
    public RecycleBinVolumeStatus GetStatus(string path) => status(path);
}

sealed class RecordingRecycleBinSender(Func<string, RecycleBinSendResult> send) : IRecycleBinSender
{
    public List<string> Sent { get; } = [];
    public List<RecycleBinSendResult> Results { get; } = [];

    public RecycleBinSendResult Send(string path, CancellationToken cancellationToken)
    {
        Sent.Add(path);
        var result = send(path);
        Results.Add(result);
        return result;
    }
}

static class RecycleBinTestCleanup
{
    // Removes only the $R item and its $I record after the name, folder, content, and original path all match.
    public static string RemoveOwnItem(string recycledPath, string originalPath, string expectedContent,
        string? recycleDirectory)
    {
        var name = Path.GetFileName(recycledPath);
        var directory = Path.GetDirectoryName(recycledPath) ?? string.Empty;
        if (!name.StartsWith("$R", StringComparison.Ordinal)
            || !directory.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase)
            || (recycleDirectory is not null
                && !string.Equals(directory, Path.TrimEndingDirectorySeparator(recycleDirectory),
                    StringComparison.OrdinalIgnoreCase)))
            return "yapılmadı: Kutu öğesi beklenen klasörde/adla değil: " + recycledPath;
        if (!File.Exists(recycledPath) || File.ReadAllText(recycledPath) != expectedContent)
            return "yapılmadı: $R içeriği eşleşmedi: " + recycledPath;
        var record = Path.Combine(directory, "$I" + name[2..]);
        if (!File.Exists(record) || !string.Equals(ReadOriginalPath(record), originalPath, StringComparison.OrdinalIgnoreCase))
            return "yapılmadı: $I kaydı yok ya da özgün yol eşleşmedi: " + record;
        File.Delete(recycledPath);
        File.Delete(record);
        return File.Exists(recycledPath) || File.Exists(record)
            ? "yapılmadı: silme sonrası dosya duruyor: " + recycledPath
            : $"tamam: {name} ve {Path.GetFileName(record)} kaldırıldı";
    }

    public static IReadOnlyList<string> FindRecordsUnder(string? recycleDirectory, string originalRoot)
    {
        if (recycleDirectory is null || !Directory.Exists(recycleDirectory))
            return [];
        var prefix = Path.TrimEndingDirectorySeparator(originalRoot) + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(recycleDirectory, "$I*")
            .Where(record => ReadOriginalPath(record)?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .Select(Path.GetFileName)
            .Select(name => name!)
            .ToArray();
    }

    // $I layout: version (8), size (8), deletion time (8); v2 then has a length-prefixed UTF-16 path, v1 a fixed 260-char path.
    private static string? ReadOriginalPath(string record)
    {
        try
        {
            var bytes = File.ReadAllBytes(record);
            if (bytes.Length < 28)
                return null;
            var version = BitConverter.ToInt64(bytes, 0);
            if (version == 2)
            {
                var length = BitConverter.ToInt32(bytes, 24);
                return length > 0 && 28 + length * 2 <= bytes.Length
                    ? Encoding.Unicode.GetString(bytes, 28, (length - 1) * 2)
                    : null;
            }
            return version == 1 ? Encoding.Unicode.GetString(bytes, 24, bytes.Length - 24).TrimEnd('\0') : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

static class NativeFileMethods
{
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    public static bool CreateJunction(string junctionPath, string targetDirectory)
    {
        Directory.CreateDirectory(junctionPath);
        using var handle = CreateFile(
            junctionPath,
            0x40000000,
            0x00000001 | 0x00000002 | 0x00000004,
            IntPtr.Zero,
            3,
            0x02000000 | 0x00200000,
            IntPtr.Zero);
        if (handle.IsInvalid)
            return false;

        var target = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var substituteName = Encoding.Unicode.GetBytes(@"\??\" + target);
        var printName = Encoding.Unicode.GetBytes(target);
        var reparseDataLength = substituteName.Length + printName.Length + 12;
        var buffer = new byte[reparseDataLength + 8];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
        BitConverter.GetBytes((ushort)reparseDataLength).CopyTo(buffer, 4);
        BitConverter.GetBytes((ushort)0).CopyTo(buffer, 8);
        BitConverter.GetBytes((ushort)substituteName.Length).CopyTo(buffer, 10);
        BitConverter.GetBytes((ushort)(substituteName.Length + 2)).CopyTo(buffer, 12);
        BitConverter.GetBytes((ushort)printName.Length).CopyTo(buffer, 14);
        substituteName.CopyTo(buffer, 16);
        printName.CopyTo(buffer, 16 + substituteName.Length + 2);
        var success = DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length,
            IntPtr.Zero, 0, out _, IntPtr.Zero);
        if (!success)
            Directory.Delete(junctionPath);
        return success;
    }

    // A sparse file reserves no clusters, so its logical size can exceed the volume without using disk space.
    public static void CreateSparseFile(string path, long length)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        if (!DeviceIoControl(stream.SafeFileHandle, 0x000900C4, [], 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new IOException("FSCTL_SET_SPARSE failed: " + Marshal.GetLastWin32Error());
        stream.SetLength(length);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        [In] byte[] inputBuffer,
        int inputBufferSize,
        IntPtr outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);
}
