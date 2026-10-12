using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace CopyPaste.App.Services;

/// <summary>
/// Başlık çubuğu ve küçült/büyüt/kapat düğmelerini koyu/mor palete uyarlar. Yüksek karşıtlık modunda
/// özel renk verilmez; sistem renkleri geçerli olur.
/// </summary>
public static class TitleBarThemeService
{
    private static readonly Color TitleBackground = ColorHelper.FromArgb(255, 0x11, 0x13, 0x19);
    private static readonly Color Text = ColorHelper.FromArgb(255, 0xF2, 0xF3, 0xF7);
    // Pasif simge: MutedBrush (#9DA5B5) sistem varsayılanından (#9DAEBA) daha soluk kaldığı için bir ton açık.
    private static readonly Color InactiveText = ColorHelper.FromArgb(255, 0xC3, 0xC8, 0xD2);
    private static readonly Color HoverBackground = ColorHelper.FromArgb(255, 0x28, 0x23, 0x3F);
    private static readonly Color PressedBackground = ColorHelper.FromArgb(255, 0x3A, 0x33, 0x60);

    /// <summary>İçeriği başlık alanına genişletilmiş pencere (ExtendsContentIntoTitleBar).</summary>
    public static void AttachExtended(Window window, AppWindow appWindow) => Attach(window, appWindow, extended: true);

    /// <summary>Sistem başlığını kullanan koyu içerikli pencere.</summary>
    public static void AttachStandard(Window window, AppWindow appWindow) => Attach(window, appWindow, extended: false);

    /// <summary>
    /// İçeriği sistem temasını izleyen pencere: başlık da uygulama modunu (açık/koyu) izler, özel renk verilmez.
    /// </summary>
    public static void AttachSystemTheme(AppWindow appWindow)
    {
        try
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
                appWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException
                                       or NotSupportedException)
        {
            // Uygulanamazsa sistem varsayılanı kalır.
        }
    }

    private static void Attach(Window window, AppWindow appWindow, bool extended)
    {
        AccessibilitySettings? accessibility = null;
        try
        {
            accessibility = new AccessibilitySettings();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or TypeLoadException)
        {
        }
        Apply(appWindow, extended, IsHighContrast(accessibility));
        if (accessibility is null)
            return;

        var dispatcher = window.DispatcherQueue;
        Windows.Foundation.TypedEventHandler<AccessibilitySettings, object> handler = (settings, _) =>
            dispatcher.TryEnqueue(() => Apply(appWindow, extended, IsHighContrast(settings)));
        try
        {
            accessibility.HighContrastChanged += handler;
        }
        catch (COMException)
        {
            return;
        }
        // Closed temsilcisi AccessibilitySettings nesnesini pencere ömrü boyunca canlı tutar.
        window.Closed += (_, _) =>
        {
            try { accessibility.HighContrastChanged -= handler; }
            catch (COMException) { }
        };
    }

    private static bool IsHighContrast(AccessibilitySettings? accessibility)
    {
        try
        {
            return accessibility?.HighContrast == true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static void Apply(AppWindow appWindow, bool extended, bool highContrast)
    {
        try
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
                return;
            var titleBar = appWindow.TitleBar;
            var custom = !highContrast;
            Color? background = custom ? TitleBackground : null;
            // Genişletilmiş başlıkta düğme zemini XAML başlık satırının kendisi; yalnız simgeler boyanır.
            Color? buttonBackground = custom ? extended ? Colors.Transparent : TitleBackground : null;
            // Önce tema, sonra açık renkler: açık renkler her durumda belirleyici kalır.
            titleBar.PreferredTheme = custom ? TitleBarTheme.Dark : TitleBarTheme.UseDefaultAppMode;
            if (!extended)
            {
                titleBar.BackgroundColor = background;
                titleBar.InactiveBackgroundColor = background;
                titleBar.ForegroundColor = custom ? Text : null;
                titleBar.InactiveForegroundColor = custom ? InactiveText : null;
            }
            titleBar.ButtonBackgroundColor = buttonBackground;
            titleBar.ButtonInactiveBackgroundColor = buttonBackground;
            titleBar.ButtonForegroundColor = custom ? Text : null;
            titleBar.ButtonInactiveForegroundColor = custom ? InactiveText : null;
            titleBar.ButtonHoverBackgroundColor = custom ? HoverBackground : null;
            titleBar.ButtonHoverForegroundColor = custom ? Colors.White : null;
            titleBar.ButtonPressedBackgroundColor = custom ? PressedBackground : null;
            titleBar.ButtonPressedForegroundColor = custom ? Colors.White : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException
                                       or NotSupportedException)
        {
            // Başlık rengi süsleme; uygulanamazsa sistem varsayılanı kalır, pencere çalışmaya devam eder.
        }
    }
}
