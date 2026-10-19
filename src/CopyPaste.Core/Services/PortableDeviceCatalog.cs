using System.Diagnostics;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>
/// Combines independent device backends in priority order. Every backend is asked for devices so a device that
/// only one of them can see still appears and diagnostics show what each one saw. Opening a device prefers its own
/// backend; when that backend fails, sees empty storage, or finds no DCIM folder, the same device is tried through
/// the other backends and the best view wins.
/// </summary>
public sealed class PortableDeviceCatalog(IReadOnlyList<IPortableDeviceSource> sources, TimeSpan? callTimeout = null)
{
    public const string DcimFolderName = "DCIM";
    private const int MaxDcimSearchDepth = 3;
    private const int MaxDcimSearchListings = 24;

    private readonly TimeSpan _callTimeout = callTimeout ?? TimeSpan.FromSeconds(45);

    public IReadOnlyList<IPortableDeviceSource> Sources { get; } = sources;

    public async Task<PortableDeviceDiscovery> DiscoverAsync(CancellationToken cancellationToken)
    {
        var reports = new List<PortableDeviceBackendReport>();
        var devices = new List<PortableDeviceInfo>();
        foreach (var source in Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsed = Stopwatch.StartNew();
            try
            {
                var found = await source.GetDevicesAsync(cancellationToken)
                    .WaitAsync(_callTimeout, cancellationToken).ConfigureAwait(false);
                reports.Add(new PortableDeviceBackendReport(source.BackendName, found, null, null, elapsed.Elapsed));
                devices.AddRange(found.Where(device => !devices.Any(known =>
                    !ReferenceEquals(known.Source, device.Source) && Matches(known, device))));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                reports.Add(new PortableDeviceBackendReport(source.BackendName, [], ex.HResult, ex.Message,
                    elapsed.Elapsed));
            }
        }
        return new PortableDeviceDiscovery(devices, reports);
    }

    public async Task<PortableDeviceOpenResult> OpenAsync(
        PortableDeviceInfo device,
        PortableDeviceDiscovery discovery,
        CancellationToken cancellationToken)
    {
        var attempts = new List<string>();
        var best = await ProbeAsync(device, attempts, cancellationToken).ConfigureAwait(false);
        foreach (var source in Sources.Where(source => !ReferenceEquals(source, device.Source)))
        {
            if (best.Outcome == PortableDeviceOpenOutcome.DcimFound)
                break;
            var alternative = FindCounterpart(device, discovery, source);
            if (alternative is null)
            {
                attempts.Add($"{source.BackendName}: no matching device");
                continue;
            }
            var candidate = await ProbeAsync(alternative, attempts, cancellationToken).ConfigureAwait(false);
            if (candidate.Outcome < best.Outcome)
                best = candidate;
        }
        return best with { Attempts = attempts };
    }

    public static string DescribePath(IEnumerable<PortableDeviceEntry> path, string? deviceName = null) =>
        string.Join(" › ", path.Select((entry, index) => index == 0 && deviceName is not null ? deviceName : entry.Name));

    /// <summary>
    /// The same physical device seen by two backends: identical device-interface path, identical name, or the only
    /// device on both sides.
    /// </summary>
    public static bool Matches(PortableDeviceInfo left, PortableDeviceInfo right)
    {
        var leftPath = InterfacePath(left.Id);
        var rightPath = InterfacePath(right.Id);
        if (leftPath is not null && rightPath is not null)
            return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static PortableDeviceInfo? FindCounterpart(
        PortableDeviceInfo device,
        PortableDeviceDiscovery discovery,
        IPortableDeviceSource source)
    {
        var report = discovery.Reports.FirstOrDefault(report => report.BackendName == source.BackendName);
        var candidates = report?.Devices.Where(candidate => ReferenceEquals(candidate.Source, source)).ToArray() ?? [];
        var match = candidates.FirstOrDefault(candidate => Matches(device, candidate));
        if (match is not null)
            return match;
        var ownCount = discovery.Reports
            .FirstOrDefault(own => own.BackendName == device.BackendName)?.Devices.Count ?? 1;
        return candidates.Length == 1 && ownCount <= 1 ? candidates[0] : null;
    }

    private static string? InterfacePath(string id)
    {
        var index = id.IndexOf(@"\\?\", StringComparison.Ordinal);
        return index >= 0 ? id[index..] : null;
    }

    private async Task<PortableDeviceOpenResult> ProbeAsync(
        PortableDeviceInfo device,
        List<string> attempts,
        CancellationToken cancellationToken)
    {
        var label = $"{device.BackendName} '{device.Name}'";
        if (device.Root is null)
        {
            var hresult = device.ErrorHResult ?? 0;
            attempts.Add($"{label}: not opened {PhoneImportErrors.FormatHResult(hresult)} {device.ErrorMessage}".TrimEnd());
            return new PortableDeviceOpenResult(device, [], PortableDeviceOpenOutcome.Failed, attempts,
                new PhoneImportFailure(device.Name, PhoneImportErrors.ClassifyHResult(hresult), hresult,
                    device.ErrorMessage ?? string.Empty));
        }

        var listings = 0;
        var anyContent = false;
        var level = new List<IReadOnlyList<PortableDeviceEntry>> { new[] { device.Root } };
        IReadOnlyList<PortableDeviceEntry> firstNonEmptyStorage = [device.Root];
        try
        {
            for (var depth = 0; depth < MaxDcimSearchDepth && level.Count > 0; depth++)
            {
                var next = new List<IReadOnlyList<PortableDeviceEntry>>();
                foreach (var path in level)
                {
                    if (listings++ >= MaxDcimSearchListings)
                        break;
                    var children = await device.Source.GetChildrenAsync(path[^1], cancellationToken)
                        .WaitAsync(_callTimeout, cancellationToken).ConfigureAwait(false);
                    if (depth > 0 && children.Count > 0 && !anyContent)
                    {
                        anyContent = true;
                        firstNonEmptyStorage = path;
                    }
                    var dcim = children.FirstOrDefault(child => child.IsFolder
                        && string.Equals(child.Name, DcimFolderName, StringComparison.OrdinalIgnoreCase));
                    if (dcim is not null)
                    {
                        var dcimPath = path.Append(dcim).ToArray();
                        attempts.Add($"{label}: DCIM found ({DescribePath(dcimPath, device.Name)})");
                        return new PortableDeviceOpenResult(device, dcimPath, PortableDeviceOpenOutcome.DcimFound,
                            attempts, null);
                    }
                    if (depth == 0 && children.Count == 0)
                    {
                        attempts.Add($"{label}: no storage visible");
                        return new PortableDeviceOpenResult(device, [device.Root],
                            PortableDeviceOpenOutcome.StorageEmpty, attempts, null);
                    }
                    next.AddRange(children.Where(child => child.IsFolder)
                        .Select(child => (IReadOnlyList<PortableDeviceEntry>)path.Append(child).ToArray()));
                }
                level = next;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var kind = PhoneImportErrors.Classify(ex);
            attempts.Add($"{label}: listing failed {PhoneImportErrors.FormatHResult(ex.HResult)} {kind} {ex.Message}");
            return new PortableDeviceOpenResult(device, [device.Root], PortableDeviceOpenOutcome.Failed, attempts,
                new PhoneImportFailure(device.Name, kind, ex.HResult, ex.Message));
        }

        attempts.Add(anyContent
            ? $"{label}: storage visible, no DCIM"
            : $"{label}: storage empty");
        return new PortableDeviceOpenResult(device, firstNonEmptyStorage,
            anyContent ? PortableDeviceOpenOutcome.StorageWithoutDcim : PortableDeviceOpenOutcome.StorageEmpty,
            attempts, null);
    }
}
