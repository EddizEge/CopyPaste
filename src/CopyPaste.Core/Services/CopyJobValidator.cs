namespace CopyPaste.Core.Services;

public sealed record ValidationResult(bool IsValid, string? Error, long? AvailableBytes)
{
    public static ValidationResult Success(long? availableBytes) => new(true, null, availableBytes);
    public static ValidationResult Failure(string error) => new(false, error, null);
}

public static class CopyJobValidator
{
    public static ValidationResult Validate(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            return ValidationResult.Failure("Kaynak ve hedef klasörlerini seçin.");

        string source;
        string destination;
        try
        {
            source = Normalize(sourcePath);
            destination = Normalize(destinationPath);
        }
        catch (Exception) when (sourcePath.Length > 0 || destinationPath.Length > 0)
        {
            return ValidationResult.Failure("Kaynak veya hedef yolu geçerli değil.");
        }

        if (!Directory.Exists(source))
            return ValidationResult.Failure("Kaynak klasör bulunamadı.");

        if (PathsOverlap(source, destination))
            return ValidationResult.Failure("Kaynak ve hedef klasörleri birbirinin içinde olamaz.");

        try
        {
            Directory.CreateDirectory(destination);
            var root = Path.GetPathRoot(destination);
            long? free = string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
            return ValidationResult.Success(free);
        }
        catch (UnauthorizedAccessException)
        {
            return ValidationResult.Failure("Hedef klasöre yazma izni yok.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return ValidationResult.Failure("Hedef klasör hazırlanamadı: " + ex.Message);
        }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static bool PathsOverlap(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        var separator = Path.DirectorySeparatorChar.ToString();
        return left.StartsWith(right + separator, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left + separator, StringComparison.OrdinalIgnoreCase);
    }
}
