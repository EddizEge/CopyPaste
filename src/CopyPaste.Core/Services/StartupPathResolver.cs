namespace CopyPaste.Core.Services;

public static class StartupPathResolver
{
    public static string? Resolve(IEnumerable<string> arguments) => arguments
        .Skip(1)
        .Select(argument => argument.Trim().Trim('"'))
        .FirstOrDefault(Directory.Exists);
}
