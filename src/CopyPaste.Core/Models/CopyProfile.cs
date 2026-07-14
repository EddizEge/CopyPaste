namespace CopyPaste.Core.Models;

public sealed record CopyProfile(
    string Id,
    string Name,
    string Description,
    int ThreadCount,
    bool UseUnbufferedIo,
    int RetryCount,
    int RetryWaitSeconds);

public static class CopyProfiles
{
    public static IReadOnlyList<CopyProfile> All { get; } =
    [
        new("balanced", "Dengeli", "Günlük kopyalamalar için hız ve kararlılık dengesi", 16, false, 3, 2),
        new("fast", "En hızlı", "SSD ve hızlı yerel diskler için yüksek paralellik", 48, false, 2, 1),
        new("large", "Büyük dosyalar", "Video, arşiv ve disk imajları için optimize", 8, true, 5, 3)
    ];
}
