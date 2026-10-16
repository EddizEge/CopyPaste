# Değişiklik günlüğü

## 1.7.5 — geliştirme aşamasında

- Ana uygulama menüsüne entegre, normal transferlerden ve zamanlamalardan işlevsel olarak tamamen ayrılmış kontrollü dosya/klasör silme bölümü eklendi.
- Geri Dönüşüm Kutusu, kalıcı silme ve desteklenen yerel sabit NTFS dosyalarında üç geçişli best-effort güvenli silme modları eklendi.
- Silmeden önce yol kanonikleştirme, iç içe seçim tekilleştirme, öğe/boyut özeti, disk kökü ve Windows/uygulama dizini koruması, reparse point sınırı ve işlem anında yeniden doğrulama eklendi.
- Kalıcı modlara yerelleştirilmiş açık uyarı ve tam ifade gerektiren ikinci onay; UAC kısıtlı hedefler için ACL/sahiplik değiştirmeyen yönetici akışı eklendi.
- Güvenli silmenin SSD/TRIM, NAS, bulut, yedek, snapshot, hard link, sıkıştırılmış ve sparse dosyalardaki sınırları açıkça gösteriliyor; disk geneli boş alan silme uygulanmıyor.
- İptal ve öğe bazlı kısmi sonuçlar, kısmen üzerine yazılmış dosyalar için yanıltıcı olmayan durumlarla raporlanıyor.
- Windows x64 portable ZIP gerçek self-contained yayın olarak üretilecek, sessiz açılış smoke testinden geçecek ve 1.7.5 sonrası her sürümde SHA-256 listesine zorunlu olarak eklenecek.
- Portable çalışma işaretçisi ve paket seçimi eklendi; uygulama içi güncelleyici portable kullanımda Setup yerine SHA-256 doğrulamalı ZIP indirip güvenli elle çıkarma için klasörünü açıyor.
- Başlık çubuğu düğmeleri koyu/mor palete göre renklendirildi: küçült/büyüt/kapat simgeleri pencere arka plandayken de okunuyor, üzerine gelme/basma durumları mor tonda; yüksek karşıtlık modunda sistem renkleri kullanılıyor. Ana pencerede uzun başlık düğmeleri kullanılıyor ve menü, dar pencerede sistem düğmelerinin altına girmek yerine kırpılıp yatay kayıyor. Silme aracının ayrı yönetici penceresinde de koyu başlık uygulanıyor. Korumalı klasör seçicinin başlığı Windows açık/koyu temasını izliyor.
- iPhone, Android telefon veya başka bir taşınabilir aygıt kaynak ya da hedef olarak seçildiğinde (klasör seçici, sürükle-bırak veya elle yazılan aygıt/kabuk yolu) alan sessizce boşaltılmıyor; Robocopy'nin bu aygıtlara doğrudan erişemediği ve dosyaların önce Fotoğraflar uygulaması ya da Dosya Gezgini ile yerel bir klasöre aktarılması gerektiği Türkçe/İngilizce açıklanıyor. Göreli yollar için tam yol isteniyor; silme aracı da yolu olmayan aygıt öğelerini sessizce yok saymak yerine bildiriyor.

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
