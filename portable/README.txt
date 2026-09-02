CopyPaste portable / taşınabilir paket
=====================================

TR
--
1. ZIP arşivini yazma izniniz olan normal bir klasöre tamamen çıkarın.
2. CopyPaste.App.exe dosyasını çalıştırın. Kurulum ve yönetici izni gerekmez.
3. Ayarlar ve yerel geçmiş, aynı Windows kullanıcısının kurulu CopyPaste sürümüyle
   paylaşılabilen %LOCALAPPDATA%\CopyPaste klasöründe tutulur.
4. Kaldırmak için CopyPaste'i kapatıp çıkardığınız klasörü silmeniz yeterlidir.
5. Setup/MSIX kimliği gerektiren kaldırma kaydı ve paketli Windows 11 Explorer
   komutları portable pakette bulunmaz.
6. İndirdiğiniz ZIP'in SHA-256 değerini sürümdeki SHA256SUMS.txt ile karşılaştırın.
7. Uygulama içi güncelleyici portable paketi algılar, Setup yerine doğrulanmış yeni
   portable ZIP'i indirir ve elle çıkarabilmeniz için dosyanın konumunu açar.

EN
--
1. Extract the entire ZIP archive to a normal folder where you have write access.
2. Run CopyPaste.App.exe. No installation or administrator rights are required.
3. Settings and local history are stored under %LOCALAPPDATA%\CopyPaste and may be
   shared with the installed CopyPaste build for the same Windows user.
4. To remove the portable build, close CopyPaste and delete the extracted folder.
5. Uninstall registration and packaged Windows 11 Explorer commands that require
   Setup/MSIX identity are not included in the portable package.
6. Compare the downloaded ZIP's SHA-256 value with SHA256SUMS.txt from the release.
7. The in-app updater detects the portable package, downloads the verified new
   portable ZIP instead of Setup, and opens its location for manual extraction.
