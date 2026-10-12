using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public enum DeletionElevationRequestError
{
    None,
    MissingRequest,
    InvalidMode,
    MissingPaths,
    TooManyPaths,
    InvalidPathEntry,
    InvalidFingerprint,
    InvalidCancelFile
}

public sealed record DeletionElevationRequestValidation(DeletionElevationRequestError Error, string Message)
{
    public bool IsValid => Error == DeletionElevationRequestError.None;
}

/// <summary>
/// Validates an elevated deletion request before the broker plans or deletes anything. The broker
/// only accepts irreversible modes; Recycle Bin deletion always runs in the medium-integrity process.
/// </summary>
public static class DeletionElevationRequestValidator
{
    public const int MaximumPathCount = 10_000;
    private const int FingerprintLength = 64;

    public static DeletionElevationRequestValidation Validate(ElevatedDeletionRequest? request)
    {
        if (request is null)
            return Fail(DeletionElevationRequestError.MissingRequest, "The elevated deletion request is empty.");
        if (!Enum.IsDefined(request.Mode)
            || request.Mode is not (DeletionMode.Permanent or DeletionMode.SecureErase))
            return Fail(DeletionElevationRequestError.InvalidMode, "The elevated deletion mode is invalid.");
        if (request.Paths is null || request.Paths.Count == 0)
            return Fail(DeletionElevationRequestError.MissingPaths, "The elevated deletion target list is empty.");
        if (request.Paths.Count > MaximumPathCount)
            return Fail(DeletionElevationRequestError.TooManyPaths, "The elevated deletion target list is too large.");
        if (request.Paths.Any(string.IsNullOrWhiteSpace))
            return Fail(DeletionElevationRequestError.InvalidPathEntry,
                "The elevated deletion target list contains an empty path.");
        if (!IsFingerprint(request.PlanFingerprint))
            return Fail(DeletionElevationRequestError.InvalidFingerprint,
                "The elevated deletion plan fingerprint is invalid.");
        if (string.IsNullOrWhiteSpace(request.CancelFile))
            return Fail(DeletionElevationRequestError.InvalidCancelFile,
                "The elevated deletion cancellation path is invalid.");
        return new(DeletionElevationRequestError.None, string.Empty);
    }

    private static bool IsFingerprint(string? value) =>
        value is { Length: FingerprintLength } && value.All(Uri.IsHexDigit);

    private static DeletionElevationRequestValidation Fail(DeletionElevationRequestError error, string message) =>
        new(error, message);
}
