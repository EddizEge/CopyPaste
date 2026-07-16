using System.Text;
using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public sealed class JobLogStore
{
    private readonly string _directory;

    public JobLogStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyPaste");
        _directory = Path.Combine(root, "Logs");
        Directory.CreateDirectory(_directory);
    }

    public async Task<string> SaveAsync(CopyJob job, IEnumerable<string> lines)
    {
        var path = Path.Combine(_directory, $"{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}-{job.Id:N}.log");
        var header = new[]
        {
            "CopyPaste transfer günlüğü",
            $"Kaynak: {job.SourcePath}",
            $"Hedef: {job.DestinationPath}",
            $"Profil: {job.Profile.Name}",
            $"Başlangıç: {job.StartedAt:O}",
            $"Bitiş: {job.CompletedAt:O}",
            $"Durum: {job.Status}",
            $"Robocopy çıkış kodu: {job.ExitCode}",
            $"Kopyalanamayan öğe: {job.FailedItemCount}",
            job.Failures.Count == 0
                ? "Kopyalanamayan öğe ayrıntısı: yok"
                : "Kopyalanamayan öğeler: " + string.Join(" | ",
                    job.Failures.Select(failure => $"{failure.Path} — {failure.Reason}")),
            string.Empty,
            "--- Robocopy çıktısı ---"
        };
        await File.WriteAllLinesAsync(path, header.Concat(lines), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
            .ConfigureAwait(false);
        return path;
    }
}
