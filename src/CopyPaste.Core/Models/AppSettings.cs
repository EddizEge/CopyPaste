namespace CopyPaste.Core.Models;

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
}
