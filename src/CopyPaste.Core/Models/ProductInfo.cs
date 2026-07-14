namespace CopyPaste.Core.Models;

public static class ProductInfo
{
    public const string GitHubOwner = "EddizEge";
    public const string GitHubRepository = "CopyPaste";
    public const string PortableAssetSuffix = "-win-x64.zip";

    public static Uri RepositoryUri { get; } =
        new($"https://github.com/{GitHubOwner}/{GitHubRepository}");

    public static Uri LatestReleaseUri { get; } =
        new($"https://github.com/{GitHubOwner}/{GitHubRepository}/releases/latest");
}
