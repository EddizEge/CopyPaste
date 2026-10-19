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
        new("1.7.6", false,
        [
            "Yeni “Telefondan aktar” bölümü, USB ile bağlı iPhone veya Android telefondaki fotoğraf ve videoları (varsayılan olarak DCIM klasörünü) alt klasör yapısını koruyarak bu bilgisayardaki bir klasöre kopyalıyor. Telefon yalnızca okunuyor; telefonda hiçbir dosya silinmiyor, değiştirilmiyor veya yeniden adlandırılmıyor.",
            "Hedefte aynı adla bulunan dosyaların üzerine yazılmıyor, bu dosyalar atlanıyor; aktarmayı yeniden başlatmak kaldığı yerden devam ediyor. Her dosya önce geçici bir adla yazılıp SHA-256 ile doğrulanıyor; iptal veya bağlantı kopması hedefte yarım dosya bırakmıyor.",
            "Başlamadan önce dosya sayısı, telefonun bildirdiği toplam boyut ve hedefteki boş alan gösteriliyor. Sonuçta aktarılan, zaten var olduğu için atlanan, başarısız ve işlenmeyen dosyalar ayrı ayrı belirtiliyor; iPhone'un “Otomatik” aktarım biçimi bir dosyayı veremezse “Orijinalleri Koru” ayarı öneriliyor.",
            "Telefona iki ayrı Windows erişim yoluyla (Windows depolama API'si ve Dosya Gezgini kabuğu) ulaşılıyor; biri telefonu veya DCIM klasörünü göremezse diğeri otomatik deneniyor. “Tanılama bilgisini kopyala” düğmesi, sorun bildirmek için aygıt ve hata bilgilerini yalnızca panoya kopyalıyor; hiçbir yere gönderilmiyor.",
            "Kaynak olarak telefon veya taşınabilir aygıt seçildiğinde gösterilen uyarı artık “Telefondan aktar…” düğmesiyle doğrudan içe aktarmaya yönlendiriyor.",
            "1.7.6 Setup ve portable ZIP paketleri, güvenilir kod imzalama sertifikası henüz bulunmadığı için Authenticode imzasız yayımlanıyor; SHA-256 değerleri yayımlanır ve imzasız MSIX dağıtılmaz."
        ],
        [
            "The new “Import from phone” section copies photos and videos from a USB-connected iPhone or Android phone (the DCIM folder by default) to a folder on this PC, preserving the subfolder structure. The phone is only read; no file on the phone is deleted, changed, or renamed.",
            "Files that already exist at the destination with the same name are skipped instead of overwritten, so running the import again resumes where it stopped. Each file is first written under a temporary name and verified with SHA-256; cancelling or losing the connection never leaves a partial file at the destination.",
            "Before starting, the file count, the total size reported by the phone, and the free space at the destination are shown. The result lists copied files, files skipped because they already exist, failed files, and files not processed; if the iPhone's “Automatic” transfer format can't deliver a file, the “Keep Originals” setting is suggested.",
            "The phone is reached through two separate Windows access paths (the Windows storage API and the File Explorer shell); if one can't see the phone or its DCIM folder, the other is tried automatically. The “Copy diagnostic information” button copies device and error details to the clipboard only, for reporting a problem; nothing is sent anywhere.",
            "The warning shown when a phone or portable device is selected as the source now offers an “Import from phone…” button that leads directly to the import.",
            "The 1.7.6 Setup and portable ZIP are published without Authenticode signatures because no trusted code-signing certificate is available yet; SHA-256 hashes are published and no unsigned MSIX is distributed."
        ]),
        new("1.7.5", false,
        [
            "Ana uygulama menüsüne entegre edilen kontrollü silme bölümü; Geri Dönüşüm Kutusu, kalıcı silme ve desteklenen yerel NTFS dosyalarında isteğe bağlı en iyi çaba güvenli silme modları sunuyor.",
            "Silme önizlemesi; çözümlenmiş hedefleri, öğe ve boyut toplamlarını, korunan yolları, reparse point sınırlarını ve depolama kısıtlarını işlemden önce doğruluyor.",
            "Kalıcı işlemler çift onay gerektiriyor; uygulama sahiplik veya ACL değiştirmiyor ve SSD/TRIM, NAS, bulut, yedek ya da snapshot bulunan ortamlarda kurtarılamazlık garantisi vermiyor.",
            "Kurulumsuz Windows x64 portable ZIP, 1.7.5 ve sonraki sürümlerde Setup ile aynı sürümü taşıyan ve SHA-256 listesine dahil edilen desteklenen bir yayın paketidir.",
            "Portable çalışmada güncelleyici Setup yerine portable ZIP'i seçiyor, SHA-256 doğrulamasından sonra güvenli elle çıkarma için indirilen klasörü açıyor.",
            "Küçült/büyüt/kapat düğmeleri pencere arka plandayken de okunuyor; başlık menüsü dar pencerede bu düğmelerin altına girmiyor ve yüksek karşıtlık modunda sistem renkleri korunuyor.",
            "iPhone, Android telefon veya başka bir taşınabilir aygıt kaynak ya da hedef seçildiğinde alan sessizce boşaltılmıyor; Robocopy'nin bu aygıtlara doğrudan erişemediği ve dosyaların önce Fotoğraflar uygulaması ya da Dosya Gezgini ile yerel bir klasöre aktarılması gerektiği açıklanıyor.",
            "Geri Dönüşüm Kutusu modu, Kutu'ya sığmayan, Kutu'su kapalı sürücüde bulunan veya yolu çok uzun olan öğeleri artık sessizce kalıcı silmiyor: önizleme bunları engelleyip açıklıyor, Kutu'ya taşınamayan öğeler sonuçta başarısız gösteriliyor, Windows hata pencereleri açılmıyor ve ilerleme çubuğu bu modda da ilerliyor.",
            "1.7.5 Setup ve portable ZIP paketleri, güvenilir kod imzalama sertifikası henüz bulunmadığı için Authenticode imzasız yayımlanıyor; SHA-256 değerleri yayımlanır ve imzasız MSIX dağıtılmaz."
        ],
        [
            "A controlled deletion section integrated into the main application menu provides Recycle Bin, permanent deletion, and optional best-effort secure erasure modes for supported local NTFS files.",
            "The deletion preview validates resolved targets, item and byte totals, protected paths, reparse-point boundaries, and storage limitations before execution.",
            "Permanent operations require two confirmations; the app does not change ownership or ACLs and makes no unrecoverability guarantee for SSD/TRIM, NAS, cloud, backup, or snapshot-backed storage.",
            "The no-install Windows x64 portable ZIP is a supported release package in 1.7.5 and later, matching the Setup version and appearing in the SHA-256 list.",
            "When running portably, the updater selects the portable ZIP instead of Setup and opens the download folder for safe manual extraction after SHA-256 verification.",
            "The minimize, maximize, and close buttons stay legible while the window is in the background; the title menu no longer slides under them in narrow windows, and high-contrast mode keeps system colors.",
            "Selecting an iPhone, Android phone, or other portable device as the source or destination no longer silently clears the field; the app explains that Robocopy can't access these devices directly and that files must first be transferred to a local folder with the Photos app or File Explorer.",
            "Recycle Bin mode no longer silently deletes items permanently when they don't fit in the Recycle Bin, are on a drive whose Recycle Bin is turned off, or have paths that are too long: the preview blocks and explains them, items that couldn't be moved to the Recycle Bin are shown as failed, no Windows error dialogs open, and the progress bar now advances in this mode too.",
            "The 1.7.5 Setup and portable ZIP are published without Authenticode signatures because no trusted code-signing certificate is available yet; SHA-256 hashes are published and no unsigned MSIX is distributed."
        ]),
        new("1.7.0", false,
        [
            "Zamanlanmış transferler düzenlenebilir, duraklatılıp yeniden etkinleştirilebilir, hemen çalıştırılabilir ve silinebilir.",
            "Kuyruk checkpoint'leri sürümlü ve atomik kaydedilir; bozuk ana kayıt son geçerli yedekten kurtarılır ve aktif ilerleme düzenli olarak saklanır.",
            "Kalıcı birim kimliğiyle USB sürücüsü varış tetikleyicisi ve yalnızca AC gücünde çalışma koşulu eklendi.",
            "NAS/ağ işleri erişilemeyen yolu, geri sayımı ve yeniden deneme sayısını gösterir; kullanıcı hemen yeniden deneyebilir ve durum yeniden başlatmada kurtarılır.",
            "Güncelleme ilerleme çubuğu, şimdi/transferlerden sonra/çıkışta kurulum seçenekleri ve standart kullanıcı kurulumları için doğrulanmış yerel geri dönüş yolu eklendi.",
            "1.7.0 Setup ve portable ZIP paketleri geçici olarak Authenticode imzasız yayımlanabilir; SHA-256 değerleri yayımlanır ve imzasız MSIX dağıtılmaz."
        ],
        [
            "Scheduled transfers can be edited, paused and resumed, run immediately, and deleted.",
            "Queue checkpoints are versioned and written atomically; a corrupt primary record recovers from the last valid backup and active progress is saved regularly.",
            "A USB-drive arrival trigger using a persistent volume identity and an AC-power-only condition were added.",
            "NAS/network jobs show the unavailable path, countdown, and retry count; users can retry immediately and the state is recovered after restart.",
            "An update progress bar, install-now/after-transfers/on-exit timing, and a verified local rollback path for standard per-user installations were added.",
            "The 1.7.0 Setup and portable ZIP may be published temporarily without Authenticode signatures; SHA-256 hashes are published and no unsigned MSIX is distributed."
        ]),
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
