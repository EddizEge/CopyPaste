# CopyPaste

CopyPaste, Windows'un yerleşik Robocopy motorunu modern ve güvenli bir arayüzle sunan dosya transfer uygulamasıdır.

## CopyPaste 1.3

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

Önerilen dağıtım dosyası `artifacts\CopyPaste-1.3.2-Setup.exe` kurulumudur. Alternatif olarak
`artifacts\CopyPaste-1.3.2-win-x64.zip` arşivini bir klasöre çıkarıp `CopyPaste.App.exe`
dosyasını doğrudan çalıştırabilirsiniz. Explorer sağ tık menüsü uygulamadaki
“Sağ tık menüsünü ekle” düğmesiyle kullanıcı hesabına kaydedilir; Windows 11'de klasik
menüler “Daha fazla seçenek göster” altında görüntülenir.

Kurumsal sessiz kurulum örneği:

```powershell
.\CopyPaste-1.3.2-Setup.exe /LANG=turkish /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="explorer"
```

İmzalı MSIX paketi `IExplorerCommand` tabanlı modern Windows 11 menüsünü içerir. CI üzerinde
`WINDOWS_CERTIFICATE_BASE64` ve `WINDOWS_CERTIFICATE_PASSWORD` sırları tanımlandığında EXE,
kurulum ve MSIX otomatik imzalanır. Sertifika yoksa kullanılabilir olmayan imzasız MSIX Release'a eklenmez.

Uygulama Windows 10 1809 ve üzerini hedefler. Robocopy Windows ile birlikte gelir; harici bir kopyalama aracı kurulmaz.

## GitHub yayını ve güncellemeler

Uygulama güncellemeleri `src/CopyPaste.Core/Models/ProductInfo.cs` içindeki GitHub depo
bilgilerinden kontrol edilir. Depo farklı bir kullanıcı adıyla yayınlanacaksa `GitHubOwner`
değerini ilk Release oluşturulmadan önce değiştirin.

- `website` klasörü bağımsız Türkçe/İngilizce tanıtım sayfasıdır.
- `.github/workflows/pages.yml`, ana dal GitHub'a gönderildiğinde siteyi GitHub Pages'a yayınlar.
- `.github/workflows/release.yml`, `v*` biçimindeki sürüm etiketi gönderildiğinde testleri çalıştırır,
  kurulum EXE'si ile taşınabilir paketi üretir ve SHA-256 özetleriyle GitHub Release'a ekler.
- Yeni bir sürüm yayımlamak için proje ve manifest sürümünü artırıp `v1.3.2` benzeri aynı sürüm
  etiketi oluşturun. Uygulama öncelikle `CopyPaste-*-Setup.exe`, bulunamazsa taşınabilir ZIP
  dosyasını indirme hedefi olarak kullanıcıya sunar.
