namespace CopyPaste.Core.Models;

public sealed record ReleaseNote(
    string Version,
    bool IsPreview,
    IReadOnlyList<string> TurkishChanges,
    IReadOnlyList<string> EnglishChanges);

public static class ReleaseNotesCatalog
{
    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new("1.6.0", false,
        [
            "Gerçek Robocopy /L önizlemesi; yeni, üzerine yazılacak, atlanacak ve hatalı dosyaları planlar.",
            "Önizlemede çözümlenmiş hedef yolu, kopyalanacak boyut ve hız sınırına bağlı süre tahmini gösterilir.",
            "Büyük önizlemeler akış halinde işlenir ve ayrıntı listesi güvenli biçimde sınırlandırılır.",
            "Uygulama içinden Türkçe ve İngilizce ayrıntılı sürüm notlarına erişilebilir.",
            "Başlık çubuğundaki sürüm rozeti gerçek uygulama sürümünü dinamik olarak gösterir.",
            "Hız sınırı ve güvenli tamamlanma eylemi her kuyruk işi için ayrı seçilebilir.",
            "Çözümlenmiş hedef yolu formda, önizlemede, kuyrukta ve aktif işte görünür.",
            "Hata listesinden yalnızca seçilen öğeler yeniden denenebilir; diğer hatalar korunur.",
            "Filtre düzenleyicisi canlı özet ve sıfırlama sunar; önizleme filtre etkisini sayı ve boyutla gösterir.",
            "Korumalı klasör seçici çoklu checkbox seçimiyle her klasör için ayrı güvenli /ZB işi hazırlar.",
            "Önizleme durumları ve gelişmiş seçeneklerin erişilebilir adı seçili dilde doğru gösterilir.",
            "Proje MIT lisansıyla açık kaynak olarak belgelendi; kod imzalama ve gizlilik politikaları eklendi.",
            "1.6.0 Setup ve portable ZIP paketleri SignPath başvurusu sonuçlanana kadar geçici olarak Authenticode imzasızdır; indirmeler SHA-256 ile doğrulanabilir ve imzasız MSIX yayımlanmaz."
        ],
        [
            "A real Robocopy /L preview plans new, overwritten, skipped, and failed files.",
            "The preview shows the resolved destination, bytes to copy, and a duration estimate when a speed limit is set.",
            "Large previews are processed as a stream and the detail list is safely bounded.",
            "Detailed release notes are available inside the app in Turkish and English.",
            "The title-bar version badge now displays the actual application version dynamically.",
            "Speed limits and safe completion actions can be selected separately for each queued job.",
            "The resolved destination is visible in the form, preview, queue, and active job.",
            "Only selected failures can be retried while unselected failures remain in the result.",
            "The filter editor provides live validation and reset controls, and preview quantifies filter effects.",
            "The protected-folder picker supports multiple checkbox selections and creates a safe /ZB job for each folder.",
            "Preview statuses and the accessible name for advanced options are shown correctly in the selected language.",
            "The project is documented as open source under the MIT License, with code-signing and privacy policies.",
            "The 1.6.0 Setup and portable ZIP are temporarily unsigned with Authenticode while the SignPath application is reviewed; downloads can be verified with SHA-256 and no unsigned MSIX is published."
        ]),
        new("1.5.1", false,
        [
            "Korumalı kaynak akışı tek UAC onayıyla yükseltilmiş CopyPaste oturumuna taşındı.",
            "Korumalı kaynak düğmesi daha anlaşılır bir + simgesiyle güncellendi."
        ],
        [
            "The protected-source flow now moves to an elevated CopyPaste session with a single UAC prompt.",
            "The protected-source button now uses a clearer + icon."
        ]),
        new("1.5.0", false,
        [
            "Seçilen klasörü veya yalnızca içeriğini kopyalama seçeneği eklendi.",
            "Korumalı klasör seçimi ve izinleri değiştirmeyen Robocopy /ZB akışı eklendi.",
            "Kaynak/hedef karşılaştırması ve yalnızca eksik veya bozuk dosyaları onarma kuyruğu eklendi.",
            "Haftalık, tek seferlik ve bilgisayar boşta olduğunda çalışan zamanlama seçenekleri eklendi.",
            "Hız sınırı, uyku engelleme ve tamamlanınca uyutma/kapatma seçenekleri eklendi.",
            "Windows başlangıcı, tepside açılma ve tek örnek davranışı eklendi."
        ],
        [
            "Added the choice to copy the selected folder or only its contents.",
            "Added protected-folder selection and a Robocopy /ZB flow that does not change permissions.",
            "Added source/destination comparison and a repair queue for missing or damaged files only.",
            "Added weekly, one-time, and idle-triggered scheduling options.",
            "Added speed limits, sleep prevention, and sleep/shutdown completion actions.",
            "Added Windows startup, start-in-tray, and single-instance behavior."
        ]),
        new("1.4.0", false,
        [
            "Windows sağ tık entegrasyonu, bildirim testi ve tanılama araçları Ayarlar'a taşındı.",
            "Otomatik, tam hız, dengeli ve düşük kaynak performans modları eklendi.",
            "Otomatik mod etkinliğe göre yükü ayarlar; düşük kaynak modu iş parçacığı, öncelik ve disk temposunu sınırlar."
        ],
        [
            "Moved Windows context-menu integration, notification testing, and diagnostics into Settings.",
            "Added automatic, full-speed, balanced, and low-resource performance modes.",
            "Automatic mode adapts to activity; low-resource mode limits threads, priority, and disk pacing."
        ]),
        new("1.3.2", false,
        [
            "Transfer seçenekleri ile uygulama tercihleri ayrıldı.",
            "Windows ile başlatma ve açılışta güncelleme denetimi eklendi.",
            "Arka plan güncelleme denetiminin düğme durumunu bozması düzeltildi."
        ],
        [
            "Separated transfer options from application preferences.",
            "Added start-with-Windows and update-check-on-startup settings.",
            "Fixed background update checks leaving the button in a busy state."
        ]),
        new("1.3.1", false,
        [
            "Gereksiz özel küçültme düğmesi kaldırıldı.",
            "Ayarlar penceresi dil, bildirim, tepsi, güncelleme, hata ve ağ seçenekleriyle geliştirildi.",
            "Arka plan güncelleme indirme durum göstergesi düzeltildi."
        ],
        [
            "Removed the unnecessary custom minimize button.",
            "Expanded Settings with language, notification, tray, update, error, and network options.",
            "Fixed the background update-download status indicator."
        ]),
        new("1.3.0", false,
        [
            "Dosya bazlı hata yeniden deneme, güvenli güncelleme, kuyruk kurtarma ve tanılama eklendi.",
            "Favoriler, özel profiller, Türkçe/İngilizce arayüz, hız/ETA, raporlar ve ağ kurtarma eklendi.",
            "Görev Zamanlayıcı, çoklu Explorer klasörü, modern bağlam menüsü ve imzalama hattı eklendi."
        ],
        [
            "Added per-file retry, secure updates, queue recovery, and diagnostics.",
            "Added favorites, custom profiles, Turkish/English UI, speed/ETA, reports, and network recovery.",
            "Added Task Scheduler support, multiple Explorer folders, a modern context menu, and signing pipelines."
        ]),
        new("1.2.0", false,
        [
            "Yeni koyu arayüz ve uygulama logosu eklendi.",
            "Robocopy kısmi başarı sınıflandırması ve hatalı öğe ayrıntıları eklendi.",
            "Setup/ZIP dağıtımı, Setup tercihli güncelleme seçimi ve akış tabanlı günlük analizi eklendi."
        ],
        [
            "Added the new dark interface and application logo.",
            "Added Robocopy partial-success classification and failed-item details.",
            "Added Setup/ZIP distribution, Setup-preferred update selection, and streaming log analysis."
        ])
    ];
}
