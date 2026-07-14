using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace CopyPaste.App.Services;

public sealed class ExplorerIntegrationService
{
    private const string LegacyDirectoryMenuPath = @"Software\Classes\Directory\shell\CopyPaste";
    private const string LegacyBackgroundMenuPath = @"Software\Classes\Directory\Background\shell\CopyPaste";
    private const string DirectoryCopyMenuPath = @"Software\Classes\Directory\shell\CopyPaste.Copy";
    private const string DirectoryPasteMenuPath = @"Software\Classes\Directory\shell\CopyPaste.Paste";
    private const string BackgroundPasteMenuPath = @"Software\Classes\Directory\Background\shell\CopyPaste.Paste";

    public bool IsRegistered => HasCommand(DirectoryCopyMenuPath)
        && HasCommand(DirectoryPasteMenuPath)
        && HasCommand(BackgroundPasteMenuPath);

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            RemoveAllMenus();
            RegisterMenu(DirectoryCopyMenuPath, "CopyPaste: Kopyala", "--copy", "%1");
            RegisterMenu(DirectoryPasteMenuPath, "CopyPaste: Buraya yapıştır", "--paste", "%1");
            RegisterMenu(BackgroundPasteMenuPath, "CopyPaste: Buraya yapıştır", "--paste", "%V");
        }
        else
        {
            RemoveAllMenus();
        }

        SHChangeNotify(ShellChangeNotifyEventId.AssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RegisterMenu(string path, string label, string action, string folderArgument)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");

        using var currentUser = OpenCurrentUser();
        using var menuKey = currentUser.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException("Explorer menü anahtarı oluşturulamadı.");
        menuKey.SetValue(string.Empty, label);
        menuKey.SetValue("Icon", executable);
        menuKey.SetValue("Position", "Top");

        using var commandKey = menuKey.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException("Explorer komut anahtarı oluşturulamadı.");
        commandKey.SetValue(string.Empty, $"\"{executable}\" {action} \"{folderArgument}\"");
    }

    private static void RemoveAllMenus()
    {
        using var currentUser = OpenCurrentUser();
        foreach (var path in new[]
                 {
                     LegacyDirectoryMenuPath,
                     LegacyBackgroundMenuPath,
                     DirectoryCopyMenuPath,
                     DirectoryPasteMenuPath,
                     BackgroundPasteMenuPath
                 })
        {
            currentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    private static bool HasCommand(string path)
    {
        using var currentUser = OpenCurrentUser();
        using var commandKey = currentUser.OpenSubKey(path + @"\command");
        return commandKey?.GetValue(string.Empty) is string command
            && command.Contains(Environment.ProcessPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static RegistryKey OpenCurrentUser() =>
        RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        ShellChangeNotifyEventId eventId,
        uint flags,
        IntPtr item1,
        IntPtr item2);

    private enum ShellChangeNotifyEventId : uint
    {
        AssocChanged = 0x08000000
    }
}
