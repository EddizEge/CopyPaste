using System.Text.Json;
using System.Security.Cryptography;

namespace CopyPaste.Core.Services;

public enum UpdateInstallTiming
{
    Now,
    AfterTransfers,
    OnExit
}

public enum UpdateRecoveryStatus
{
    Prepared,
    InstallerLaunched,
    HealthCheckPending,
    Healthy,
    RolledBack
}

public sealed record UpdateRecoveryState(
    int SchemaVersion,
    string PreviousVersion,
    string TargetVersion,
    string InstallDirectory,
    string BackupDirectory,
    string InstallerPath,
    IReadOnlyDictionary<string, string> FileHashes,
    UpdateInstallTiming? InstallTiming,
    UpdateRecoveryStatus Status,
    DateTimeOffset PreparedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdateRecoveryPreparation(
    bool Success,
    UpdateRecoveryState? State = null,
    UpdateRecoveryError Error = UpdateRecoveryError.None,
    string? ErrorDetail = null);

public enum UpdateRecoveryError
{
    None,
    UnsupportedInstallLocation,
    InstalledAppNotFound,
    InstallerNotFound,
    BackupFailed
}

public sealed class UpdateRecoveryService
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _rootDirectory;
    private readonly string _statePath;
    private readonly string _allowedInstallDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public UpdateRecoveryService(string? rootDirectory = null, string? allowedInstallDirectory = null)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CopyPaste", "Updates"));
        _statePath = Path.Combine(_rootDirectory, "update-recovery.json");
        _allowedInstallDirectory = Path.GetFullPath(allowedInstallDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "CopyPaste"));
    }

    public string StatePath => _statePath;

    public async Task<UpdateRecoveryPreparation> PrepareAsync(
        string currentAppDirectory,
        string installerPath,
        string previousVersion,
        string targetVersion,
        CancellationToken cancellationToken = default)
    {
        var installDirectory = Path.GetFullPath(currentAppDirectory);
        var verifiedInstaller = Path.GetFullPath(installerPath);
        if (!PathsEqual(installDirectory, _allowedInstallDirectory))
            return new(false, Error: UpdateRecoveryError.UnsupportedInstallLocation);
        if (!File.Exists(Path.Combine(installDirectory, "CopyPaste.App.exe")))
            return new(false, Error: UpdateRecoveryError.InstalledAppNotFound);
        if (!File.Exists(verifiedInstaller))
            return new(false, Error: UpdateRecoveryError.InstallerNotFound);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            var rollbackRoot = Path.Combine(_rootDirectory, "Rollback");
            Directory.CreateDirectory(rollbackRoot);
            var backupDirectory = Path.Combine(
                rollbackRoot, $"{SanitizeVersion(previousVersion)}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupDirectory);
            try
            {
                await CopyDirectoryAsync(
                    installDirectory, backupDirectory, cancellationToken).ConfigureAwait(false);
                var fileHashes = await CalculateHashesAsync(
                    backupDirectory, cancellationToken).ConfigureAwait(false);
                var now = DateTimeOffset.UtcNow;
                var state = new UpdateRecoveryState(
                    CurrentSchemaVersion,
                    previousVersion,
                    targetVersion,
                    installDirectory,
                    backupDirectory,
                    verifiedInstaller,
                    fileHashes,
                    null,
                    UpdateRecoveryStatus.Prepared,
                    now,
                    now);
                await WriteStateUnsafeAsync(state, cancellationToken).ConfigureAwait(false);
                return new(true, state);
            }
            catch
            {
                TryDeleteDirectory(backupDirectory);
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, Error: UpdateRecoveryError.BackupFailed, ErrorDetail: ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateRecoveryState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_statePath))
                return null;
            await using var stream = new FileStream(_statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var state = await JsonSerializer.DeserializeAsync<UpdateRecoveryState>(
                stream, _options, cancellationToken).ConfigureAwait(false);
            return IsValidState(state) ? state : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<UpdateRecoveryState?> MarkInstallerLaunchedAsync(CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(UpdateRecoveryStatus.InstallerLaunched, cancellationToken);

    public Task<UpdateRecoveryState?> MarkPreparedAsync(CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(UpdateRecoveryStatus.Prepared, cancellationToken);

    public async Task<UpdateRecoveryState?> SetInstallTimingAsync(
        UpdateInstallTiming timing,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!IsValidState(state) || state is null)
                return null;
            var updated = state with { InstallTiming = timing, UpdatedAt = DateTimeOffset.UtcNow };
            await WriteStateUnsafeAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<UpdateRecoveryState?> BeginHealthCheckAsync(CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(UpdateRecoveryStatus.HealthCheckPending, cancellationToken);

    public Task<UpdateRecoveryState?> MarkHealthyAsync(CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(UpdateRecoveryStatus.Healthy, cancellationToken);

    public async Task<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!IsValidState(state) || state is null
                || state.Status is UpdateRecoveryStatus.RolledBack)
                return false;
            if (!await VerifyHashesAsync(
                    state.BackupDirectory, state.FileHashes, cancellationToken).ConfigureAwait(false))
                return false;
            await CopyDirectoryAsync(
                state.BackupDirectory, state.InstallDirectory, cancellationToken).ConfigureAwait(false);
            await WriteStateUnsafeAsync(
                state with { Status = UpdateRecoveryStatus.RolledBack, UpdatedAt = DateTimeOffset.UtcNow },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UpdateRecoveryState?> ChangeStatusAsync(
        UpdateRecoveryStatus status,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!IsValidState(state) || state is null)
                return null;
            var updated = state with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
            await WriteStateUnsafeAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UpdateRecoveryState?> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
            return null;
        await using var stream = new FileStream(_statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return await JsonSerializer.DeserializeAsync<UpdateRecoveryState>(
                stream, _options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task WriteStateUnsafeAsync(
        UpdateRecoveryState state,
        CancellationToken cancellationToken)
    {
        var temporaryPath = _statePath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream, state, _options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, _statePath, overwrite: true);
    }

    private bool IsValidState(UpdateRecoveryState? state)
    {
        if (state is not
            {
                SchemaVersion: CurrentSchemaVersion,
                InstallDirectory.Length: > 0,
                BackupDirectory.Length: > 0,
                FileHashes.Count: > 0
            })
            return false;
        try
        {
            return PathsEqual(state.InstallDirectory, _allowedInstallDirectory)
                   && IsWithinRoot(state.BackupDirectory, Path.Combine(_rootDirectory, "Rollback"))
                   && Directory.Exists(state.BackupDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                continue;
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var targetPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
            if (!IsWithinRoot(targetPath, destinationRoot))
                throw new IOException("Rollback backup path escaped its safe directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var source = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var target = new FileStream(
                targetPath, FileMode.Create, FileAccess.Write, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(directory, file);
            await using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            hashes[relativePath] = Convert.ToHexString(
                await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }
        return hashes;
    }

    private static async Task<bool> VerifyHashesAsync(
        string directory,
        IReadOnlyDictionary<string, string> expectedHashes,
        CancellationToken cancellationToken)
    {
        foreach (var (relativePath, expectedHash) in expectedHashes)
        {
            var file = Path.GetFullPath(Path.Combine(directory, relativePath));
            if (!IsWithinRoot(file, directory) || !File.Exists(file))
                return false;
            await using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar)
            .Equals(Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

    private static string SanitizeVersion(string version) =>
        string.Concat(version.Where(character => char.IsLetterOrDigit(character) || character is '.' or '-'));

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
