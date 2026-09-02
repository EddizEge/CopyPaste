using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

public static class PortableDistributionDetector
{
    public static bool IsPortable(string applicationDirectory)
    {
        if (string.IsNullOrWhiteSpace(applicationDirectory))
            return false;
        try
        {
            var directory = Path.GetFullPath(applicationDirectory);
            return File.Exists(Path.Combine(directory, ProductInfo.PortableMarkerFileName));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
