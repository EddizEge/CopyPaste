using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CopyPaste.App.Services;

public static class LocalizationService
{
    private static readonly IReadOnlyList<(string Tr, string En)> Texts =
    [
        ("Güncellemeler", "Updates"), ("Güncellemeleri kontrol et", "Check for updates"),
        ("Robocopy ile hızlı ve doğrulanabilir transfer", "Fast, verifiable transfers powered by Robocopy"),
        ("Geçmiş", "History"), ("Zamanlama", "Scheduling"), ("Ayarlar", "Settings"),
        ("Yeni transfer", "New transfer"), ("Kaynak ve hedefi seçin, kuyruğa ekleyin.", "Choose source and destination, then add to queue."),
        ("Klasörü pencereye sürükleyerek kaynak seçebilirsiniz", "Drag a folder onto the window to select a source"),
        ("Kaynak", "Source"), ("Hedef", "Destination"), ("Profil", "Profile"),
        ("Kopyalanacak klasör", "Folder to copy"), ("Dosyaların kopyalanacağı klasör", "Destination folder"),
        ("＋  Kuyruğa ekle", "＋  Add to queue"), ("Kuyruk", "Queue"), ("Transfer kuyruğu", "Transfer queue"),
        ("Kuyruğu başlat", "Start queue"),
        ("Temizle", "Clear"), ("Kuyruk boş. Yukarıdan bir transfer ekleyin.", "Queue is empty. Add a transfer above."),
        ("Hataları yeniden dene", "Retry errors"), ("Kaldır", "Remove"),
        ("Kopyalanamayan öğeler", "Items not copied"),
        ("Diğer dosyalar işlendi. Bu öğeleri inceleyebilir veya yeniden deneyebilirsiniz.", "Other files were processed. Review or retry these items."),
        ("Listeyi kopyala", "Copy list"), ("Günlüğü aç", "Open log"), ("Rapor", "Report"), ("Yeniden dene", "Retry"),
        ("Transfer ayarları", "Transfer settings"), ("Mevcut dosyalar", "Existing files"), ("Doğrulama", "Verification"),
        ("Gelişmiş seçenekler", "Advanced options"), ("Dosya filtreleri", "File filters"),
        ("Hariç tutulacak klasörler", "Excluded folders"), ("Hata olsa da sıradaki işe geç", "Continue to next job after an error"),
        ("Transfer sonunda bildirim göster", "Show notification when transfer finishes"),
        ("Aktif işte kapatılırsa tepsiye küçült", "Minimize to tray when closed during a transfer"),
        ("Güncellemeleri güvenle arka planda indir", "Securely download updates in the background"),
        ("Ağ koparsa bağlantının geri gelmesini bekle", "Wait for the connection to return after a network outage"),
        ("Ağ için en fazla bekleme (dakika)", "Maximum network wait (minutes)"),
        ("Ayarları kaydet", "Save settings"), ("Transfer ayarlarını kaydet", "Save transfer settings"),
        ("Windows entegrasyonu", "Windows integration"),
        ("Sağ tık menüsünü ekle", "Add context menu"), ("Sağ tık menüsünü kaldır", "Remove context menu"),
        ("Bildirimi test et", "Test notification"), ("Son işlem", "Last operation"),
        ("Uyarılarla tamamlandı", "Completed with warnings"),
        ("İşlem hatalarla birlikte tamamlandı", "Operation completed with errors"),
        ("Transfer tamamlandı", "Transfer completed"), ("Transfer iptal edildi", "Transfer cancelled"),
        ("Transfer duraklatıldı", "Transfer paused"), ("Transfer başarısız", "Transfer failed"),
        ("Önceki oturum beklenmedik şekilde kapandı. Tanılama paketini Windows entegrasyonu bölümünden oluşturabilirsiniz.",
            "The previous session closed unexpectedly. You can create a diagnostics package from Windows integration."),
        ("Tanılama paketi", "Diagnostics package"),
        ("Henüz tamamlanan işlem yok", "No completed operation yet"), ("İlk transferiniz burada görünecek.", "Your first transfer will appear here."),
        ("HTML / CSV raporu oluştur", "Create HTML / CSV report"), ("Güvenli varsayılanlar", "Safe defaults"),
        ("✓ Kesintiden devam eden /Z modu", "✓ Restartable /Z mode"),
        ("✓ MIR ve PURGE kapalı — kaynak dosyaları silinmez", "✓ MIR and PURGE disabled — source files are never deleted"),
        ("✓ Hatalı dosyalar ayrı listelenir ve yeniden denenebilir", "✓ Failed files are listed separately and can be retried"),
        ("Dil", "Language"), ("Türkçe", "Turkish"), ("İngilizce", "English"),
        ("Açık", "On"), ("Kapalı", "Off"),
        ("İptal", "Cancel"), ("Duraklat", "Pause"), ("İndir", "Download")
    ];

    public static bool IsEnglish(string? language) =>
        language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;

    public static string Translate(string value, string? language)
    {
        var english = IsEnglish(language);
        foreach (var (tr, en) in Texts)
        {
            if (value.Equals(tr, StringComparison.Ordinal) || value.Equals(en, StringComparison.Ordinal))
                return english ? en : tr;
        }
        return value;
    }

    public static void Apply(DependencyObject root, string? language)
    {
        switch (root)
        {
            case TextBlock textBlock when !string.IsNullOrWhiteSpace(textBlock.Text):
                textBlock.Text = Translate(textBlock.Text, language);
                break;
            case Button button when button.Content is string content:
                button.Content = Translate(content, language);
                break;
            case ToggleSwitch toggle when toggle.Header is string header:
                toggle.Header = Translate(header, language);
                toggle.OnContent = IsEnglish(language) ? "On" : "Açık";
                toggle.OffContent = IsEnglish(language) ? "Off" : "Kapalı";
                break;
            case Expander expander when expander.Header is string header:
                expander.Header = Translate(header, language);
                break;
            case TextBox textBox when !string.IsNullOrWhiteSpace(textBox.PlaceholderText):
                textBox.PlaceholderText = Translate(textBox.PlaceholderText, language);
                break;
        }
        if (root is FrameworkElement element && ToolTipService.GetToolTip(element) is string tooltip)
            ToolTipService.SetToolTip(element, Translate(tooltip, language));
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
            Apply(VisualTreeHelper.GetChild(root, index), language);
    }
}
