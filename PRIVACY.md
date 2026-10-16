# Gizlilik politikası / Privacy policy

## Türkçe

CopyPaste telemetri, analiz, reklam veya kullanıcı hesabı içermez. Kopyalanan dosyalar,
dosya yolları, transfer geçmişi, raporlar ve günlükler CopyPaste geliştiricisine veya
CopyPaste tarafından işletilen bir sunucuya gönderilmez.

Uygulama aşağıdaki durumlarda ağ bağlantısı kullanabilir:

- Varsayılan olarak açık olan başlangıçta güncelleme kontrolü, en son sürüm bilgisini
  almak için GitHub API'ye standart bir HTTPS isteği gönderir. Bu seçenek Ayarlar'dan
  kapatılabilir.
- Yeni bir sürüm bulunduğunda güncelleme paketi (kurulu kullanımda Setup, portable
  kullanımda ZIP) varsayılan olarak arka planda GitHub Releases üzerinden indirilir ve
  GitHub'ın yayın bilgisinde bildirdiği SHA-256 özetiyle doğrulanır. Otomatik indirme
  Ayarlar'dan kapatılabilir; kapalıyken paket yalnızca kullanıcı indirmeyi seçtiğinde alınır.
- Kullanıcının seçtiği ağ/NAS kaynakları ve hedefleri Windows ile Robocopy tarafından
  doğrudan kullanılır; bu veriler CopyPaste geliştiricisine aktarılmaz.
- Kullanıcının açmayı seçtiği proje, sürüm veya destek bağlantıları varsayılan web
  tarayıcısında GitHub'ı açabilir.

GitHub bağlantıları GitHub'ın kendi [gizlilik bildirimine](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
tabidir. Uygulama ayarları, kuyruk kurtarma verileri, transfer geçmişi ve raporlar
kullanıcının Windows profilinde yerel olarak saklanır.

## English

CopyPaste contains no telemetry, analytics, advertising, or user accounts. Copied
files, file paths, transfer history, reports, and logs are not sent to the CopyPaste
developer or to any server operated by CopyPaste.

The application can use network connections in the following cases:

- The startup update check, enabled by default, sends a standard HTTPS request to the
  GitHub API to retrieve the latest release information. It can be disabled in Settings.
- When a new release is found, the update package (Setup for installed use, the ZIP for
  portable use) is downloaded in the background from GitHub Releases by default and
  verified against the SHA-256 digest reported in GitHub's release information. Automatic
  download can be turned off in Settings; when it is off, the package is retrieved only
  when the user chooses to download it.
- Network or NAS sources and destinations selected by the user are accessed directly
  by Windows and Robocopy; this data is not transferred to the CopyPaste developer.
- Project, release, or support links opened by the user can open GitHub in the default
  web browser.

Connections to GitHub are subject to GitHub's own [privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).
Application settings, queue recovery data, transfer history, and reports are stored
locally in the user's Windows profile.
