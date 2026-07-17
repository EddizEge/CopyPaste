namespace CopyPaste.Core.Models;

public sealed record FavoriteLocation(string Name, string Path);

public sealed record AppSettings
{
    public string DefaultProfileId { get; init; } = "balanced";
    public ExistingFileBehavior ExistingFiles { get; init; } = ExistingFileBehavior.Update;
    public VerificationMode Verification { get; init; } = VerificationMode.Size;
    public string FilePatterns { get; init; } = "*";
    public string ExcludedDirectories { get; init; } = string.Empty;
    public bool ContinueQueueOnError { get; init; } = true;
    public bool NotificationsEnabled { get; init; } = true;
    public bool MinimizeToTrayWhileRunning { get; init; } = true;
    public bool AutoDownloadUpdates { get; init; } = true;
    public string Language { get; init; } = "tr-TR";
    public IReadOnlyList<FavoriteLocation> FavoriteLocations { get; init; } = [];
    public IReadOnlyList<string> RecentSources { get; init; } = [];
    public IReadOnlyList<string> RecentDestinations { get; init; } = [];
    public IReadOnlyList<CopyProfile> CustomProfiles { get; init; } = [];
    public bool WaitForNetwork { get; init; } = true;
    public int NetworkRetryMinutes { get; init; } = 15;
}
