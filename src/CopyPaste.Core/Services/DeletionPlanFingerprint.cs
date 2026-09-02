using System.Security.Cryptography;
using System.Text;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class DeletionPlanFingerprint
{
    public static string Create(DeletionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((int)plan.Mode);
            writer.Write(plan.RequestedPaths.Count);
            foreach (var path in plan.RequestedPaths)
                writer.Write(path);

            writer.Write(plan.Entries.Count);
            foreach (var entry in plan.Entries)
            {
                writer.Write(entry.Path);
                writer.Write((int)entry.Kind);
                writer.Write(entry.Length);
                writer.Write((int)entry.Attributes);
                writer.Write(entry.LastWriteTimeUtc.UtcDateTime.Ticks);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))))
            .ToLowerInvariant();
    }
}
