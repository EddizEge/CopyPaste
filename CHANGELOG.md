# Değişiklik günlüğü

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
