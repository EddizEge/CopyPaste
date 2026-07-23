# Değişiklik günlüğü

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
