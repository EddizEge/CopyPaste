using System.Diagnostics;
using System.Globalization;
using System.Text;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class RobocopyCommandBuilder
{
    public static ProcessStartInfo Build(CopyJob job)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "robocopy.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = GetRobocopyEncoding(),
            StandardErrorEncoding = GetRobocopyEncoding()
        };

        info.ArgumentList.Add(job.SourcePath);
        info.ArgumentList.Add(job.DestinationPath);
        foreach (var pattern in job.Options.FilePatterns)
            info.ArgumentList.Add(pattern);
        info.ArgumentList.Add("/E");
        info.ArgumentList.Add(job.UseBackupMode ? "/ZB" : "/Z");
        info.ArgumentList.Add("/COPY:DAT");
        info.ArgumentList.Add("/DCOPY:DAT");
        var threadCount = job.ActivePerformanceMode switch
        {
            TransferPerformanceMode.FullSpeed => job.Profile.ThreadCount,
            TransferPerformanceMode.LowResource => Math.Min(job.Profile.ThreadCount, 4),
            _ => Math.Min(job.Profile.ThreadCount, 16)
        };
        info.ArgumentList.Add($"/MT:{Math.Clamp(threadCount, 1, 128)}");
        info.ArgumentList.Add($"/R:{job.Profile.RetryCount}");
        info.ArgumentList.Add($"/W:{job.Profile.RetryWaitSeconds}");
        info.ArgumentList.Add("/BYTES");
        info.ArgumentList.Add("/FP");
        info.ArgumentList.Add("/TEE");
        info.ArgumentList.Add("/XJ");

        if (job.ActivePerformanceMode == TransferPerformanceMode.LowResource)
            info.ArgumentList.Add("/IPG:25");
        if (job.BandwidthLimitMbps > 0)
            info.ArgumentList.Add($"/IORATE:{Math.Clamp(job.BandwidthLimitMbps, 1, 10240)}m");

        if (job.Profile.UseUnbufferedIo)
            info.ArgumentList.Add("/J");

        switch (job.Options.ExistingFiles)
        {
            case ExistingFileBehavior.Skip:
                info.ArgumentList.Add("/XC");
                info.ArgumentList.Add("/XN");
                info.ArgumentList.Add("/XO");
                break;
            case ExistingFileBehavior.Overwrite:
                info.ArgumentList.Add("/IS");
                info.ArgumentList.Add("/IT");
                break;
        }

        if (job.Options.ExcludedDirectories.Count > 0)
        {
            info.ArgumentList.Add("/XD");
            foreach (var directory in job.Options.ExcludedDirectories)
                info.ArgumentList.Add(directory);
        }

        return info;
    }

    private static Encoding GetRobocopyEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Console.OutputEncoding;
        }
    }
}
