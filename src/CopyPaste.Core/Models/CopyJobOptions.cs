namespace CopyPaste.Core.Models;

public enum ExistingFileBehavior
{
    Update,
    Skip,
    Overwrite
}

public enum VerificationMode
{
    None,
    Size,
    Sha256
}

public sealed record CopyJobOptions
{
    public ExistingFileBehavior ExistingFiles { get; init; } = ExistingFileBehavior.Update;
    public VerificationMode Verification { get; init; } = VerificationMode.Size;
    public IReadOnlyList<string> FilePatterns { get; init; } = ["*"];
    public IReadOnlyList<string> ExcludedDirectories { get; init; } = [];
}
