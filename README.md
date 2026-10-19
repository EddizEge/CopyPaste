# CopyPaste

CopyPaste, Windows'un yerleşik Robocopy motorunu modern ve güvenli bir arayüzle sunan dosya transfer uygulamasıdır.

CopyPaste, [MIT License](LICENSE) ile açık kaynak olarak yayımlanır. Ayrıntılar için
[Code signing policy](CODE_SIGNING_POLICY.md) ve [gizlilik politikası](PRIVACY.md)
belgelerine bakabilirsiniz.

## CopyPaste 1.7.6

- Düzeltme: 1.7.5 Setup ve portable ZIP paketlerinde uygulamanın kaynak dosyaları
  (`CopyPaste.App.pri`, `*.xbf`) eksikti ve uygulama açılışta kapanıyordu. 1.7.5
  kuruluysa ve açılmıyorsa 1.7.6'yı GitHub'dan indirip kurun; açılamayan sürüm
  kendini güncelleyemez
- Başlık menüsünde ayrı “Telefondan aktar” bölümü: USB ile bağlı iPhone veya Android
  telefondaki seçilen klasör (iPhone'da `DCIM`) alt klasör yapısı korunarak bu
  bilgisayardaki bir klasöre kopyalanır
- Telefon yalnızca okunur; telefonda hiçbir dosya silinmez, değiştirilmez veya yeniden
  adlandırılmaz
- Hedefte aynı adla var olan dosyalar atlanır, üzerine yazılmaz; yeniden çalıştırmak
  kaldığı yerden devam eder
- Her dosya geçici adla yazılıp SHA-256 ile doğrulandıktan sonra son adına taşınır;
  iptal veya bağlantı kopması hedefte yarım dosya bırakmaz
- Başlamadan önce dosya sayısı, bildirilen toplam boyut ve hedefteki boş alan; sonuçta
  aktarılan, atlanan, başarısız ve işlenmeyen dosyalar
- İki Windows erişim yolu (WinRT depolama API'si ve Dosya Gezgini kabuğu) ile otomatik
  yedek; sorun bildirmek için yalnızca panoya kopyalanan tanılama bilgisi

Son kararlı sürüm `v1.7.6`'dır. `v1.7.6` Setup EXE ve portable ZIP paketleri
Authenticode imzasızdır; ayrıntılar aşağıdadır.

### Telefondan aktarma

1. Telefonu USB kablosuyla bağlayın ve kilidini açın. iPhone “Bu bilgisayara
   güvenilsin mi?” diye sorarsa “Güven”e dokunun; Android'de USB bildiriminden
   “Dosya aktarımı”nı seçin.
2. CopyPaste'te başlık menüsünden “Telefondan aktar”ı açın, listeden telefonu seçin
   (görünmüyorsa “Yenile”). Telefon Windows'ta Dosya Gezgini'nin “Bu bilgisayar”
   bölümünde görünmelidir.
3. Telefondaki klasörü seçin (bulunursa `DCIM` varsayılan olarak açılır), bu
   bilgisayardaki hedef klasörü seçin ve “Özeti hazırla”ya basın.
4. Özeti kontrol edip “Aktarmayı başlat”a basın. Sonuçtan “Hedef klasörü aç” veya
   eksik dosyalar için “Yeniden dene”yi kullanabilirsiniz.

Sorun olursa: iPhone dosyaları “Otomatik” aktarım biçiminde dönüştürüp
veremeyebilir; iPhone'da Ayarlar > Fotoğraflar > “Mac'e veya PC'ye Aktar” >
“Orijinalleri Koru”yu seçip yeniden deneyin. Sorunu bildirirken “Tanılama bilgisini
kopyala” düğmesiyle panoya alınan metni ekleyebilirsiniz.

## CopyPaste 1.7.5

- Ana uygulama menüsüne entegre, normal transferlerden işlevsel olarak ayrılmış kontrollü dosya ve klasör silme bölümü
- Varsayılan Geri Dönüşüm Kutusu, kalıcı silme ve desteklenen yerel NTFS dosyalarında
  üç geçişli best-effort güvenli silme modları
- Disk kökü, Windows/uygulama dizini, hard link ve reparse point güvenlik sınırları;
  kalıcı modlarda çift onay ve gerektiğinde ACL değiştirmeyen UAC akışı
- SSD/TRIM, NAS, bulut, yedek ve snapshot ortamlarında kurtarılamazlık garantisi
  verilemeyeceğini belirten Türkçe/İngilizce uyarılar
- Kurulum gerektirmeyen gerçek self-contained Windows x64 portable ZIP ve sessiz
  paket smoke testi
- Kurulu kullanımda Setup'ı, portable kullanımda SHA-256 doğrulamalı ZIP'i seçen
  dağıtım biçimine duyarlı güncelleme akışı
- Geri Dönüşüm Kutusu modunda Kutu'ya taşınamayan öğeleri yerinde bırakan ve taşındığı
  doğrulanamayan öğeyi açıkça bildiren koruma
- Telefon veya taşınabilir aygıt seçildiğinde Türkçe/İngilizce açıklama
- Başlık çubuğu düğmelerinde daha iyi okunurluk ve dar pencerede başlık menüsü

## CopyPaste 1.6

- Seçilen klasörü hedefe klasör olarak kopyalama veya yalnızca içeriğini kopyalama; önerilen varsayılan klasörün kendisidir
- Harici Windows disklerindeki korumalı kaynakları sahiplik ve ACL değiştirmeden seçen yönetici gezgini ve Robocopy `/ZB` modu
- Kaynak-hedef önizleme, boyut/SHA-256 karşılaştırması ve yalnızca eksik ya da bozuk dosyaları onarma
- Günlük, haftalık, tek seferlik ve bilgisayar boşta olduğunda çalışan zamanlamalar
- Sayısal aktarım hız sınırı, transfer süresince uyku engelleme ve tamamlanınca uyutma/kapatma seçeneği
- Windows başlangıcında doğrudan sistem tepsisinde açılma ve çalışan örneğe yönlendiren tek uygulama örneği

- Yalnızca kopyalanamayan dosyaları yeniden deneme
- GitHub güncellemesini arka planda indirme ve zorunlu SHA-256 doğrulaması
- Beklenmedik kapanma sonrası kuyruğu duraklatılmış olarak kurtarma
- Favori klasörler, son kullanılan yollar ve özel profiller
- Çalışma zamanında Türkçe/İngilizce arayüz değişimi
- Canlı hız, aktarılan bayt, tamamlanan dosya ve tahmini kalan süre
- HTML işlem raporu ve CSV hata listesi
- NAS/ağ bağlantısı kopunca bekleme ve `/Z` ile otomatik sürdürme
- Windows Görev Zamanlayıcı ile günlük transferler
- Birden fazla Explorer klasörünü tek seferde kuyruğa gönderme
- İmzalı MSIX sürümünde Windows 11 modern sağ tık menüsü
- Çökme tanılama paketi ve aktif transferde güvenli çıkış onayı

- Web sitesiyle uyumlu koyu, kart tabanlı ve mor vurgulu yeni arayüz
- Görev çubuğu, pencere, EXE ve kurulumda kullanılan yeni CopyPaste simgesi
- Kısmi başarı desteği: az sayıdaki dosya hatası tüm transferi başarısız göstermez
- Kopyalanamayan dosya, hata nedeni ve hata kodunu uygulama içinde görüntüleme
- Hatalı öğe listesini kopyalama, günlüğü açma ve transferi yeniden deneme
- Tek tıkla kurulan, kullanıcı hesabına özel Windows Setup EXE
- GitHub Releases güncellemelerinde kurulum dosyasını otomatik tercih etme

- Dengeli, En hızlı ve Büyük dosyalar profilleri
- Yeniden başlatılabilir Robocopy transferleri
- Otomatik, tam hız, dengeli ve düşük kaynak performans modları
- Tam ekran uygulama algılama ve boşta tam hız davranışı
- Güvenli ve izin listeli komut oluşturma
- Kaynak/hedef çakışması ve yazma izni kontrolü
- Canlı Robocopy çıktısı ve dosya yüzdesi
- İptal desteği
- Son 100 işlemin yerel geçmişi
- Windows açık/koyu tema desteği
- Açılışta GitHub Releases üzerinden sessiz güncelleme kontrolü
- Yeni sürüm bulunduğunda uygulama içi uyarı ve Windows bildirimi
- Başlık çubuğundan manuel güncelleme kontrolü
- Türkçe/İngilizce ürün ve indirme sitesi

## İkinci aşama

- Birden fazla transferi sıraya ekleme ve sırayla çalıştırma
- Dosya/klasör sayısı, toplam boyut ve hedef boş alanı için ön analiz
- Hedefte zaten bulunan dosyalar için çakışma özeti
- Kuyruk öğelerinde canlı durum ve ilerleme
- Klasörü pencereye sürükleyerek kaynak seçme
- Kuyruğun tamamını durdurabilen aktif iş iptali

## Windows entegrasyonu

- Transfer tamamlandığında yerel Windows bildirimi
- Kullanıcı isteğiyle sistem tepsisine küçültme
- Aktif transfer sırasında pencere kapatılırsa işi tepside sürdürme
- Kullanıcı hesabına özel Explorer sağ tık menüsü
- Sağ tık ile seçilen klasörü kaynak alanına otomatik aktarma
- Single-project MSIX manifesti ve uygulama görselleri

## Gelişmiş kontrol ve doğrulama

- Hedefteki mevcut dosyaları güncelleme, atlama veya üzerine yazma davranışı
- Noktalı virgülle ayrılan güvenli dosya filtreleri (`*.jpg;*.png` gibi)
- Ada göre klasör hariç tutma (`node_modules;.git;temp` gibi)
- Hızlı dosya boyutu doğrulaması
- İsteğe bağlı tam SHA-256 içerik doğrulaması
- Filtre ve hariç tutmaları dikkate alan ön analiz

## Tam ürün özellikleri

- Kuyruk öğelerini yukarı/aşağı taşıma, kaldırma ve başarısız işi yeniden deneme
- Aktif transferi duraklatma ve Robocopy `/Z` desteğiyle daha sonra devam ettirme
- Kuyruğun hata sonrasında devam edip etmeyeceğini seçme
- Profil, çakışma, doğrulama ve filtre tercihlerinin kalıcı ayarlarda saklanması
- Her transfer için `%LOCALAPPDATA%\CopyPaste\Logs` altında ayrıntılı Robocopy günlüğü
- Geçmişten bir transferi forma geri yükleme ve günlük dosyasını açma
- Geçmişi uygulama içinden temizleme
- Bildirim ve aktif işte tepsiye küçültme davranışını ayarlama
- Kurulum gerektirmeyen, kendi çalışma zamanını içeren Windows x64 taşınabilir paket

## Doğrulanan senaryolar

- 100 küçük Unicode dosya ve derin klasör yolları
- 8 MB ikili dosya
- Kaynak/hedef SHA-256 eşleşmesi
- Aynı transferi güvenle tekrar çalıştırma
- İki işlik kuyruğun sıralı tamamlanması
- On binlerce dosyalı Robocopy çıktısında akış tabanlı hata ayrıştırma
- Kilitli tek dosyada transferin “hatalarla tamamlandı” sonucuna geçmesi

## Geliştirme

Depo, `.dotnet` altında yerel .NET 8 SDK ile çalışabilir:

```powershell
$env:DOTNET_ROOT = "$PWD\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet build CopyPaste.sln -p:Platform=x64
dotnet run --project tests/CopyPaste.Core.Tests
dotnet run --project src/CopyPaste.App -p:Platform=x64
```

Dağıtılabilir paketi üretmek için:

```powershell
.\tools\Build-Release.ps1
.\tools\Build-Installer.ps1
.\tools\Build-Msix.ps1
```

Geliştirme paketlerinde önerilen dosya `artifacts\CopyPaste-1.7.6-Setup.exe` kurulumudur.
Alternatif olarak `artifacts\CopyPaste-1.7.6-win-x64.zip` arşivini yazma izniniz olan
bir klasöre tamamen çıkarıp `CopyPaste.App.exe`
dosyasını doğrudan çalıştırabilirsiniz. Explorer sağ tık menüsü uygulamadaki
“Sağ tık menüsünü ekle” düğmesiyle kullanıcı hesabına kaydedilir; Windows 11'de klasik
menüler “Daha fazla seçenek göster” altında görüntülenir.

Kurumsal sessiz kurulum örneği:

```powershell
.\CopyPaste-1.7.6-Setup.exe /LANG=turkish /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="explorer"
```

`v1.6.0`, `v1.7.0`, `v1.7.5` ve `v1.7.6` Setup EXE ve portable ZIP paketleri ürün sahibinin
sürüm bazlı istisnalarıyla Authenticode imzasız yayımlandı. İndirmeler `SHA256SUMS.txt`
ile doğrulanmalıdır; imzasız MSIX yayımlanmaz. Bu istisnalar sonraki sürümlere
taşınmaz. SignPath Foundation başvurusu 24 Temmuz 2026'da proje henüz yeterli dış
görünürlük sinyaline sahip olmadığı için reddedildi. Güvenilir MSIX dağıtımı için
Microsoft Store hazırlığı 1.8 yol haritasındadır; doğrudan EXE/Setup imzalama ayrı kalır.

## Code signing policy

SignPath Foundation sponsorluğu şu anda etkin değildir. Gelecekteki güvenilir imzalama,
Microsoft Store MSIX veya ayrıca onaylanmış bir Authenticode sağlayıcısı üzerinden
uygulanacaktır. Sürüm onayı ve doğrulama adımları
[kod imzalama politikasında](CODE_SIGNING_POLICY.md) açıklanır. Uygulamanın ağ davranışı
için [gizlilik politikasına](PRIVACY.md) bakın.

Uygulama Windows 10 1809 ve üzerini hedefler. Robocopy Windows ile birlikte gelir; harici bir kopyalama aracı kurulmaz.

## GitHub yayını ve güncellemeler

Uygulama güncellemeleri `src/CopyPaste.Core/Models/ProductInfo.cs` içindeki GitHub depo
bilgilerinden kontrol edilir. Depo farklı bir kullanıcı adıyla yayınlanacaksa `GitHubOwner`
değerini ilk Release oluşturulmadan önce değiştirin.

- `website` klasörü bağımsız Türkçe/İngilizce tanıtım sayfasıdır.
- `.github/workflows/pages.yml`, ana dal GitHub'a gönderildiğinde siteyi GitHub Pages'a yayınlar.
- `.github/workflows/release.yml`, `v*` biçimindeki sürüm etiketi gönderildiğinde testleri çalıştırır,
  kurulum EXE'si ile taşınabilir paketi üretir ve SHA-256 özetleriyle GitHub Release'a ekler.
- Geçici imzasız yayın istisnası yalnızca `v1.6.0`, `v1.7.0`, `v1.7.5` veya `v1.7.6` etiketi ile
  `ALLOW_UNSIGNED_RELEASE=true` değişkeni birlikteyken çalışır; sonraki sürümler dahil
  diğer bütün sürümler güvenilir imzalama olmadan durur.
- Yeni bir sürüm yayımlamak için proje ve manifest sürümünü artırıp `v1.7.6` benzeri aynı sürüm
  etiketi oluşturun. Uygulama kurulu dağıtımda `CopyPaste-*-Setup.exe`, portable dağıtımda
  `CopyPaste-*-win-x64.zip` paketini tercih eder; yalnızca tercih edilen paket yoksa diğerini
  indirme hedefi olarak kullanır.
