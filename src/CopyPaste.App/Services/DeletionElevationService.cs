using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using CopyPaste.Core.Models;
using CopyPaste.Core.Services;

namespace CopyPaste.App.Services;

public enum ElevatedDeletionFailureKind
{
    RequestRejected,
    BrokerFailed,
    NoResult
}

public sealed record ElevatedDeletionResponse(
    DeletionResult? Result,
    string? Error,
    ElevatedDeletionFailureKind? FailureKind = null);

public sealed class ElevatedDeletionException(ElevatedDeletionFailureKind kind, string message)
    : InvalidOperationException(message)
{
    public ElevatedDeletionFailureKind Kind { get; } = kind;
}

public static class DeletionElevationService
{
    private const int MaximumRequestBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool LaunchElevatedTool(string language)
    {
        try
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("CopyPaste executable path could not be determined.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas"
            };
            startInfo.ArgumentList.Add("--elevated-deletion-tool");
            startInfo.ArgumentList.Add("--language");
            startInfo.ArgumentList.Add(language);
            return Process.Start(startInfo) is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    public static async Task<DeletionResult> ExecuteAsync(
        DeletionPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (plan.Mode == DeletionMode.RecycleBin)
            return await new DeletionService().ExecuteAsync(plan, cancellationToken: cancellationToken);
        if (IsElevated())
            return await new DeletionService().ExecuteAsync(plan, cancellationToken: cancellationToken);

        var directory = RequestDirectory();
        Directory.CreateDirectory(directory);
        var operationId = Guid.NewGuid().ToString("N");
        var requestFile = Path.Combine(directory, $"{operationId}.request.json");
        var responseFile = Path.Combine(directory, $"{operationId}.response.json");
        var cancelFile = Path.Combine(directory, $"{operationId}.cancel");
        try
        {
            var request = new ElevatedDeletionRequest(
                plan.RequestedPaths,
                plan.Mode,
                DeletionPlanFingerprint.Create(plan),
                cancelFile);
            await File.WriteAllTextAsync(requestFile, JsonSerializer.Serialize(request, JsonOptions),
                cancellationToken).ConfigureAwait(false);
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("CopyPaste executable path could not be determined.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas"
            };
            startInfo.ArgumentList.Add("--elevated-delete-request");
            startInfo.ArgumentList.Add(requestFile);
            startInfo.ArgumentList.Add("--elevated-delete-result");
            startInfo.ArgumentList.Add(responseFile);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The elevated deletion process could not be started.");
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try { File.WriteAllText(cancelFile, string.Empty); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            });
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var response = await ReadResponseAsync(responseFile).ConfigureAwait(false);
            if (response.Result is not null)
                return response.Result;
            throw new ElevatedDeletionException(
                response.FailureKind ?? ElevatedDeletionFailureKind.BrokerFailed,
                response.Error ?? "The elevated deletion process failed.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(plan.Mode,
                plan.RequestedPaths.Select(path => new DeletionItemResult(
                    path, false, false, DeletionIssueCode.AccessDenied,
                    "Administrator approval was canceled.")).ToArray(),
                true);
        }
        finally
        {
            DeleteQuietly(requestFile);
            DeleteQuietly(responseFile);
            DeleteQuietly(cancelFile);
        }
    }

    private static async Task<ElevatedDeletionResponse> ReadResponseAsync(string responseFile)
    {
        try
        {
            if (!File.Exists(responseFile))
                throw new ElevatedDeletionException(ElevatedDeletionFailureKind.NoResult,
                    "The elevated deletion process did not return a result.");
            return JsonSerializer.Deserialize<ElevatedDeletionResponse>(
                       await File.ReadAllTextAsync(responseFile, CancellationToken.None).ConfigureAwait(false),
                       JsonOptions)
                   ?? throw new ElevatedDeletionException(ElevatedDeletionFailureKind.NoResult,
                       "The elevated deletion process returned an empty result.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new ElevatedDeletionException(ElevatedDeletionFailureKind.NoResult, ex.Message);
        }
    }

    public static async Task<bool> TryHandleElevatedRequestAsync(string[] commandLine)
    {
        var requestFile = GetArgumentValue(commandLine, "--elevated-delete-request");
        if (requestFile is null)
            return false;
        var responseFile = GetArgumentValue(commandLine, "--elevated-delete-result");
        if (responseFile is null)
            return true;

        ElevatedDeletionResponse response;
        var deletionStarted = false;
        try
        {
            EnsureBrokerPath(requestFile, ".request.json");
            EnsureBrokerPath(responseFile, ".response.json");
            var requestInfo = new FileInfo(requestFile);
            if (!requestInfo.Exists || requestInfo.Length is <= 0 or > MaximumRequestBytes
                || (requestInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The elevated deletion request is invalid.");
            var request = JsonSerializer.Deserialize<ElevatedDeletionRequest>(
                await File.ReadAllTextAsync(requestFile).ConfigureAwait(false), JsonOptions);
            var validation = DeletionElevationRequestValidator.Validate(request);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);
            EnsureBrokerPath(request!.CancelFile, ".cancel");

            var planner = new DeletionPlanner();
            var plan = await planner.CreateAsync(request.Paths, request.Mode).ConfigureAwait(false);
            if (!plan.IsValid
                || !DeletionPlanFingerprint.Create(plan).Equals(
                    request.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                var failures = plan.Issues.Count > 0
                    ? plan.Issues.Select(issue => new DeletionItemResult(
                        issue.Path, false, false, issue.Code, issue.Message)).ToArray()
                    : request.Paths.Select(path => new DeletionItemResult(
                        path, false, false, DeletionIssueCode.ChangedAfterPreview,
                        "A target changed after the deletion preview; nothing was deleted.")).ToArray();
                response = new(new DeletionResult(request.Mode, failures, false), null);
            }
            else
            {
                using var cancellationSource = new CancellationTokenSource();
                var monitor = MonitorCancellationFileAsync(request.CancelFile, cancellationSource);
                deletionStarted = true;
                var result = await new DeletionService(planner)
                    .ExecuteAsync(plan, cancellationToken: cancellationSource.Token).ConfigureAwait(false);
                cancellationSource.Cancel();
                await monitor.ConfigureAwait(false);
                response = new(result, null);
            }
        }
        catch (Exception ex)
        {
            // The broker must always answer and exit; an escaped exception would crash async void OnLaunched.
            response = new(null, ex.Message, deletionStarted
                ? ElevatedDeletionFailureKind.BrokerFailed
                : ElevatedDeletionFailureKind.RequestRejected);
        }

        try
        {
            EnsureBrokerPath(responseFile, ".response.json");
            await File.WriteAllTextAsync(responseFile,
                JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Without a response file the medium-integrity side reports an unknown result.
        }
        return true;
    }

    private static async Task MonitorCancellationFileAsync(
        string cancelFile,
        CancellationTokenSource cancellationSource)
    {
        while (!cancellationSource.IsCancellationRequested)
        {
            if (File.Exists(cancelFile))
            {
                cancellationSource.Cancel();
                return;
            }
            try
            {
                await Task.Delay(250, cancellationSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static void EnsureBrokerPath(string path, string expectedSuffix)
    {
        if (!Path.IsPathFullyQualified(path)
            || !path.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The elevated deletion broker path is invalid.");
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetFullPath(RequestDirectory()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The elevated deletion broker path is outside its private directory.");
    }

    private static string RequestDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CopyPaste", "DeletionRequests");

    private static string? GetArgumentValue(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, argument =>
            argument.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    private static void DeleteQuietly(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
