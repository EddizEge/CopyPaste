# Değişiklik günlüğü

## 1.7.6

- Düzeltme: 1.7.5 Setup ve portable ZIP paketlerinde uygulamanın kaynak dizini (`CopyPaste.App.pri`) ve derlenmiş XAML dosyaları (`App.xbf`, `MainWindow.xbf`) eksikti; ana pencere yüklenemiyor ve uygulama açılışta kapanıyordu. 1.7.5'te paketleme `dotnet publish` çıktısına geçmişti ve paketlenmemiş WinUI yayını bu dosyaları yayın klasörüne koymuyor. Paketleme artık bu dosyaları aynı yayın derlemesinin çıktısından pakete ekliyor; paket smoke testi ana pencerenin XAML'ını da yüklüyor ve dosyalar eksikse yayın oluşturulmuyor. 1.7.5 kuruluysa ve açılmıyorsa uygulama içi güncelleyici çalışamaz: 1.7.6 Setup'ı veya portable ZIP'i GitHub'dan indirilmelidir. GitHub'daki v1.7.5 sürümü 19 Ekim 2026'da ön sürüm olarak işaretlendi; böylece 1.7.0 kullanıcılarına güncelleme olarak sunulmuyor.
- Başlık menüsüne ayrı bir “Telefondan aktar” çalışma alanı eklendi: USB ile bağlı iPhone veya Android telefon seçilip telefondaki bir klasör (bulunursa varsayılan olarak `DCIM`) bu bilgisayardaki bir klasöre alt klasör yapısı korunarak kopyalanıyor (`DCIM\100APPLE\IMG_0001.HEIC` → `<hedef>\100APPLE\IMG_0001.HEIC`). Telefon yalnızca okunuyor: silme, değiştirme veya yeniden adlandırma yapan hiçbir çağrı kullanılmıyor; Windows'un “aktardıktan sonra sil” seçeneği olan içe aktarma API'si hiç kullanılmıyor. Kaynağın (yol listesi, boyut, tarihler, öznitelikler, SHA-256) tam aktarma, yeniden çalıştırma, iptal ve okuma hatasından sonra birebir aynı kaldığı otomatik testlerle doğrulanıyor.
- Hedefte aynı göreli yolda aynı adlı dosya varsa üzerine yazılmıyor, “zaten var” diye atlanıyor; iPhone'un bildirdiği boyut gerçek bayttan farklı olabildiği için karar boyuta göre verilmiyor. Böylece yeniden çalıştırmak kaldığı yerden devam ediyor.
- Her dosya önce aynı klasörde geçici adla (`.<ad>.cptmp-<rastgele>`) yazılıyor, diske boşaltılıyor, okunan baytlarla SHA-256'sı eşleşirse üzerine yazmadan son adına taşınıyor; eşleşmezse siliniyor ve başarısız sayılıyor. Hata, iptal veya bağlantı kopmasında geçici dosya siliniyor; son ad bu arada oluşmuşsa var olan dosyaya dokunulmuyor. Dosyanın değiştirilme ve oluşturma zamanı telefonun bildirdiği tarihe ayarlanıyor.
- NTFS'te geçersiz karakterler, sondaki nokta/boşluk, ayrılmış aygıt adları (`CON`, `LPT1`...), büyük/küçük harf çakışmaları ve 255 karakteri aşan adlar kararlı biçimde güvenli adlara çevriliyor; uzun hedef yolları destekleniyor.
- Başlamadan önce dosya sayısı, bildirilen toplam boyut, zaten var olanlar ve hedefteki boş alan gösteriliyor; alan yetmiyorsa açık uyarı veriliyor ve kullanıcı bilerek yine de başlatabiliyor. İlerleme dosya n/N, bayt ve geçerli dosya adıyla en çok 100 ms aralıkla bildiriliyor; iptal dosyalar arasında ve kopyalama döngüsünde çalışıyor.
- Öğe hatasında aktarma sürüyor; aygıt koptuğunda (ör. `0x8007048F`, `0x80070015`, `0x80070079`, `0x800701B1`, `0x80070490`) ve aygıt artık yanıt vermiyorsa ya da hedef dolduğunda duruluyor, kalanlar “işlenmedi” olarak bildiriliyor. `0x8007001F` hatasında iPhone için “Ayarlar > Fotoğraflar > Mac'e veya PC'ye Aktar > Orijinalleri Koru” ipucu gösteriliyor. Sonuç ekranı aktarılan, atlanan, başarısız (ilk 20'si yol ve sebebiyle) ve işlenmeyen sayılarını, “Hedef klasörü aç” ve “Yeniden dene” düğmelerini gösteriyor.
- Telefona iki bağımsız yoldan erişiliyor: birincil olarak WinRT `StorageDevice` (yolu olan USB bellekler hariç), yedek olarak Dosya Gezgini kabuğu (`IShellItem`, ayrılmış STA iş parçacığında). Birincil yol hata verir, aygıtı göremez veya depolamayı/DCIM'i göremezse diğer yol otomatik deneniyor; kullanılan yol sonuçta ve tanılamada görünüyor, istenirse elle seçilebiliyor.
- Telefon kilitliyse veya “Bu bilgisayara güven” onaylanmadıysa ve hiç aygıt yoksa Türkçe/İngilizce yönergeler gösteriliyor. “Tanılama bilgisini kopyala” düğmesi uygulama ve Windows sürümünü, her erişim yolunun gördüğü aygıtları, DCIM durumunu, ilk hataları ve HRESULT'ları ve son işlemin sayılarını yalnızca panoya kopyalıyor; aygıt kimliklerindeki seri numarası gibi benzersiz bölümler “…” ile gizleniyor.
- Kaynak olarak telefon veya taşınabilir aygıt seçildiğinde (klasör seçici veya sürükle-bırak) gösterilen uyarıya “Telefondan aktar…” düğmesi eklendi; uyarı, dosyaların önce Telefondan aktar ile yerel bir klasöre kopyalanıp sonra o klasörün kaynak seçilmesini anlatıyor. Seçicinin döndürdüğü telefon klasörü doğrudan içe aktarma kaynağı olarak açılabiliyor.
- `v1.7.6` Setup ve portable ZIP paketleri, güvenilir kod imzalama sertifikası bulunmadığı için ürün sahibinin 19 Ekim 2026 tarihli sürüme özel onayıyla açık uyarı ve SHA-256 doğrulamasıyla Authenticode imzasız yayımlanıyor; imzasız MSIX yayımlanmıyor. Bu istisna sonraki sürümlere taşınmaz.

## 1.7.5

- Ana uygulama menüsüne entegre, normal transferlerden ve zamanlamalardan işlevsel olarak tamamen ayrılmış kontrollü dosya/klasör silme bölümü eklendi.
- Geri Dönüşüm Kutusu, kalıcı silme ve desteklenen yerel sabit NTFS dosyalarında üç geçişli best-effort güvenli silme modları eklendi.
- Silmeden önce yol kanonikleştirme, iç içe seçim tekilleştirme, öğe/boyut özeti, disk kökü ve Windows/uygulama dizini koruması, reparse point sınırı ve işlem anında yeniden doğrulama eklendi.
- Kalıcı modlara yerelleştirilmiş açık uyarı ve tam ifade gerektiren ikinci onay; UAC kısıtlı hedefler için ACL/sahiplik değiştirmeyen yönetici akışı eklendi.
- Güvenli silmenin SSD/TRIM, NAS, bulut, yedek, snapshot, hard link, sıkıştırılmış ve sparse dosyalardaki sınırları açıkça gösteriliyor; disk geneli boş alan silme uygulanmıyor.
- İptal ve öğe bazlı kısmi sonuçlar, kısmen üzerine yazılmış dosyalar için yanıltıcı olmayan durumlarla raporlanıyor.
- Windows x64 portable ZIP gerçek self-contained yayın olarak üretiliyor ve sessiz açılış smoke testinden geçiyor; 1.7.5 ve sonraki her sürümde ZIP ve SHA-256 girdisi zorunlu, eksikse yayın otomasyonu duruyor.
- Portable çalışma işaretçisi ve paket seçimi eklendi; uygulama içi güncelleyici portable kullanımda Setup yerine SHA-256 doğrulamalı ZIP indirip güvenli elle çıkarma için klasörünü açıyor.
- Başlık çubuğu düğmeleri koyu/mor palete göre renklendirildi: küçült/büyüt/kapat simgeleri pencere arka plandayken de okunuyor, üzerine gelme/basma durumları mor tonda; yüksek karşıtlık modunda sistem renkleri kullanılıyor. Ana pencerede uzun başlık düğmeleri kullanılıyor ve menü, dar pencerede sistem düğmelerinin altına girmek yerine kırpılıp yatay kayıyor. Silme aracının ayrı yönetici penceresinde de koyu başlık uygulanıyor. Korumalı klasör seçicinin başlığı Windows açık/koyu temasını izliyor.
- iPhone, Android telefon veya başka bir taşınabilir aygıt kaynak ya da hedef olarak seçildiğinde (klasör seçici, sürükle-bırak veya elle yazılan aygıt/kabuk yolu) alan sessizce boşaltılmıyor; Robocopy'nin bu aygıtlara doğrudan erişemediği ve dosyaların önce Fotoğraflar uygulaması ya da Dosya Gezgini ile yerel bir klasöre aktarılması gerektiği Türkçe/İngilizce açıklanıyor. Göreli yollar için tam yol isteniyor; silme aracı da yolu olmayan aygıt öğelerini sessizce yok saymak yerine bildiriyor.
- Geri Dönüşüm Kutusu modu artık Kutu'ya gönderilemeyen öğeleri sessizce kalıcı silmiyor. Önizleme; Kutu'su kapalı sürücüyü (Windows ayarı `NukeOnDelete` veya `NoRecycleFiles` ilkesi), Kutu kapasitesini aşan kökü ve 260 karakter sınırına ulaşan yolu kayıt defterini yalnız okuyarak engelliyor ve bilerek “Kalıcı silme” seçilebileceğini söylüyor; değer okunamazsa engellemiyor. Silme, Visual Basic `FileSystem` yerine kabuğun `IFileOperation` arabirimiyle her kök için ayrı ve pencere açmadan yapılıyor: Kutu'ya taşınmayacak alt öğeler reddediliyor, öğe Kutu'da doğrulanamadan kaybolursa sonuç bunu açıkça bildirip sonraki köklere dokunmuyor. Sonuç ekranı Kutu'ya taşınamayan öğeleri iki dilde başarısız gösteriyor; ilerleme çubuğu her kökte ilerliyor.
- `v1.7.5` Setup ve portable ZIP paketleri, güvenilir kod imzalama sertifikası bulunmadığı için ürün sahibinin 16 Ekim 2026 tarihli sürüme özel onayıyla açık uyarı ve SHA-256 doğrulamasıyla Authenticode imzasız yayımlanıyor; imzasız MSIX yayımlanmıyor. Bu istisna sonraki sürümlere taşınmaz.

## 1.7.0

- Zamanlanmış transfer yönetimi genişletildi: kayıtlı görevler düzenlenebilir, duraklatılıp yeniden etkinleştirilebilir, hemen çalıştırılabilir ve silinebilir.
- Kuyruk kayıtları sürümlü ve atomik checkpoint biçimine taşındı; bozuk ana kayıt son geçerli yedekten kurtarılıyor ve aktif transfer ilerlemesi beş saniyede bir saklanıyor.
- Kalıcı birim kimliğiyle USB sürücüsü bağlanınca görev başlatma ve yalnızca AC gücünde çalıştırma koşulu eklendi.
- NAS/ağ bekleme durumu kuyruğa ve checkpoint'e yansıtıldı; erişilemeyen yol, geri sayım, deneme sayısı, “Şimdi yeniden dene” ve beş kontrollü devam denemesi eklendi.
- Güncelleme indirmesine görsel ilerleme çubuğu; şimdi, transferlerden sonra veya çıkışta kurma seçenekleri; standart kullanıcı kurulumları için yerel yedek, sağlık kontrolü ve geri dönüş akışı eklendi.
- `v1.7.0` Setup ve portable ZIP paketleri için SignPath başvurusu sonuçlanana kadar açık uyarı ve SHA-256 doğrulamalı geçici Authenticode imzasız yayın istisnası onaylandı; imzasız MSIX yayımlanmayacak.

## 1.6.0

- “Önizle / karşılaştır” akışı gerçek Robocopy `/L` kuru çalışmasını kullanacak şekilde geliştirildi; yeni, üzerine yazılacak, atlanacak ve hatalı dosya sayıları ile kopyalanacak boyut gösteriliyor.
- Önizleme, çözümlenmiş gerçek hedef yolunu gösteriyor ve hız sınırı tanımlı işler için süre tahmini üretiyor.
- Büyük önizlemelerde Robocopy çıktısı akış halinde işleniyor; ayrıntı listesi ilk 500 öğeyle sınırlanırken özet tüm planı kapsıyor.
- Uygulama içinden Türkçe ve İngilizce ayrıntılı sürüm notlarına erişilebilen yeni bir pencere eklendi.
- Başlık çubuğundaki sabit `1.5` rozeti kaldırıldı; gerçek uygulama sürümü dinamik olarak gösteriliyor.
- Hız sınırı ve uyutma/kapatma eylemi her kuyruk işi için ayrı seçilebilir hale getirildi; güç eylemi yalnızca tüm çalıştırma başarıyla bittiğinde son işten uygulanıyor.
- Çözümlenmiş gerçek hedef yolu forma yazarken, önizlemede, kuyrukta ve aktif işte görünür hale getirildi.
- Hata listesinden seçilen öğeleri yeniden deneme eklendi; seçilmeyen hatalar sonuç listesinden kaybolmuyor.
- Filtre düzenleyicisine canlı geçerlilik özeti, açıklamalar ve sıfırlama eklendi; önizleme filtre dışında kalan dosya sayısı ve boyutunu gösteriyor.
- Korumalı kaynak seçici çoklu checkbox seçimini destekliyor ve seçilen her klasörü izinleri değiştirmeden ayrı `/ZB` kuyruk işi olarak hazırlıyor.
- Önizleme tamamlandıktan sonra durum satırının “Karşılaştırılıyor” olarak kalması düzeltildi; gelişmiş seçeneklerin erişilebilir adı seçili dile uyarlanıyor.
- Proje MIT lisansıyla açık kaynak olarak belgelendi; SignPath Foundation başvurusu için kod imzalama ve gizlilik politikaları eklendi.
- `v1.6.0` Setup ve portable ZIP paketlerinin SignPath başvurusu sonuçlanana kadar açık uyarı ve SHA-256 doğrulamasıyla geçici olarak Authenticode imzasız yayımlanmasına karar verildi; imzasız MSIX yayımlanmıyor.

## 1.5.1

- Korumalı kaynak akışı tek UAC onayıyla yükseltilmiş bir CopyPaste oturumuna taşındı; böylece Robocopy `/ZB` yedekleme yetkisi gerçek transfer sırasında da kullanılabilir.
- Korumalı kaynak düğmesi daha anlaşılır bir `+` simgesiyle güncellendi.

## 1.5.0

- Seçilen klasörün kendisini veya yalnızca içeriğini kopyalama seçeneği eklendi; varsayılan davranış klasörün kendisini kopyalamaktır.
- Korumalı harici Windows disklerinde sahiplik/izin değiştirmeden gezinmek için yönetici kaynak seçicisi ve Robocopy `/ZB` desteği eklendi.
- Kaynak ile hedefi boyut veya SHA-256 ile karşılaştıran kuru çalışma ekranı ve yalnızca eksik/bozuk dosyaları onarma kuyruğu eklendi.
- Günlük zamanlamaya haftalık, tek seferlik ve bilgisayar boşta olduğunda çalıştırma seçenekleri eklendi.
- Sayısal hız sınırı, transfer boyunca Windows uykusunu önleme ve tamamlanınca uyutma/kapatma seçenekleri eklendi.
- Windows başlangıcında tepside açılma ve ikinci çalıştırmaları mevcut pencereye yönlendiren tek örnek davranışı eklendi.

## 1.4.0

- Windows sağ tık entegrasyonu, bildirim testi ve tanılama araçları ana ekrandan Ayarlar'a taşındı.
- Otomatik, tam hız, dengeli ve düşük kaynak performans modları eklendi.
- Otomatik mod tam ekran uygulamalarda Robocopy yükünü düşürür, bilgisayar boşta olduğunda tam hıza çıkar.
- Düşük kaynak modunda iş parçacığı, işlem önceliği ve disk erişim temposu sınırlandırılır.

## 1.3.2

- Transfer seçenekleri ile uygulama tercihleri birbirinden ayrıldı.
- Ayarlar'a Windows ile başlatma ve açılışta güncelleme denetimi seçenekleri eklendi.
- Otomatik güncelleme denetiminin düğmede “Kontrol ediliyor…” göstermesi kaldırıldı.

## 1.3.1

- Başlık çubuğundaki gereksiz özel küçültme düğmesi kaldırıldı.
- Ayarlar düğmesi; dil, bildirim, tepsi, güncelleme, hata ve ağ tercihlerini içeren çalışan bir pencereye dönüştürüldü.
- Arka plandaki güncelleme indirmesi sırasında düğmenin sürekli “Kontrol ediliyor…” göstermesi düzeltildi.

## 1.3.0

- Hatalı dosya bazlı yeniden deneme ve Türkçe dosya adları için OEM Robocopy çıktı düzeltmesi.
- SHA-256 zorunlu güvenli güncelleme indirme ve kurulum başlatma.
- Çökme sonrası kuyruk kurtarma, aktif işte çıkış koruması ve tanılama paketi.
- Favoriler, son kullanılan klasörler, özel profiller ve Türkçe/İngilizce arayüz.
- Canlı hız, tahmini kalan süre, HTML/CSV raporlar ve NAS bağlantı kurtarma.
- Windows Görev Zamanlayıcı ile günlük işler ve çoklu Explorer klasörü desteği.
- İmzalı MSIX için `IExplorerCommand` tabanlı Windows 11 modern sağ tık komutları.
- İsteğe bağlı Authenticode/MSIX kod imzalama GitHub Actions hattı.

## 1.2.0

- Web sitesiyle aynı görsel dili kullanan yeni koyu arayüz ve yeni uygulama logosu.
- Robocopy 8–15 sonuçları için kısmi başarı durumu ve kullanıcı dostu sonuç özeti.
- Kopyalanamayan dosyaları, Windows hata açıklamasını ve hata kodunu gösteren ayrıntı paneli.
- Hata listesini panoya kopyalama, günlüğü açma ve başarısız transferi yeniden deneme.
- GitHub Releases için kullanıcı hesabına kurulan Inno Setup EXE ve taşınabilir ZIP.
- Güncelleme kontrolünde Setup EXE'yi otomatik tercih etme.
- Büyük Robocopy günlüklerinde belleği koruyan akış tabanlı hata analizi.
