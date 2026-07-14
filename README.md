# CopyPaste

CopyPaste, Windows'un yerleşik Robocopy motorunu modern ve güvenli bir arayüzle sunan dosya transfer uygulamasıdır.

## CopyPaste 1.1

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
- Taşınabilir, kendi çalışma zamanını içeren Windows x64 yayın paketi

## Doğrulanan senaryolar

- 100 küçük Unicode dosya ve derin klasör yolları
- 8 MB ikili dosya
- Kaynak/hedef SHA-256 eşleşmesi
- Aynı transferi güvenle tekrar çalıştırma
- İki işlik kuyruğun sıralı tamamlanması

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
```

Çıktı `artifacts\CopyPaste-1.1.0-win-x64.zip` altında oluşur. Arşivi bir klasöre çıkarıp
`CopyPaste.App.exe` dosyasını çalıştırmak yeterlidir. Explorer sağ tık menüsü uygulamadaki
“Sağ tık menüsünü ekle” düğmesiyle kullanıcı hesabına kaydedilir; Windows 11'de klasik
menüler “Daha fazla seçenek göster” altında görüntülenir.

Uygulama Windows 10 1809 ve üzerini hedefler. Robocopy Windows ile birlikte gelir; harici bir kopyalama aracı kurulmaz.

## GitHub yayını ve güncellemeler

Uygulama güncellemeleri `src/CopyPaste.Core/Models/ProductInfo.cs` içindeki GitHub depo
bilgilerinden kontrol edilir. Depo farklı bir kullanıcı adıyla yayınlanacaksa `GitHubOwner`
değerini ilk Release oluşturulmadan önce değiştirin.

- `website` klasörü bağımsız Türkçe/İngilizce tanıtım sayfasıdır.
- `.github/workflows/pages.yml`, ana dal GitHub'a gönderildiğinde siteyi GitHub Pages'a yayınlar.
- `.github/workflows/release.yml`, `v*` biçimindeki sürüm etiketi gönderildiğinde testleri çalıştırır,
  taşınabilir paketi üretir ve GitHub Release'a ekler.
- Yeni bir sürüm yayımlamak için proje ve manifest sürümünü artırıp `v1.1.0` benzeri aynı sürüm
  etiketi oluşturun. Uygulama, Release içindeki `CopyPaste-*-win-x64.zip` dosyasını indirme hedefi
  olarak kullanıcıya sunar.
