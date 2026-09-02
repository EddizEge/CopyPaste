# Code signing policy / Kod imzalama politikası

## Türkçe

CopyPaste'in SignPath Foundation açık kaynak sponsorluğu başvurusu 24 Temmuz 2026'da,
proje henüz yeterli dış görünürlük ve topluluk sinyaline sahip olmadığı için reddedildi.
SignPath imzalama şu anda etkin değildir. Proje daha geniş ve doğrulanabilir benimsenme
kazandığında yeniden başvuru yapılabilir; ücretli bir hizmet veya sertifika ayrıca ürün
sahibi onayı gerektirir. Microsoft Store'a gönderilen MSIX'in Store tarafından imzalanması
1.8 dağıtım planında, doğrudan EXE/Setup Authenticode imzasından ayrı ele alınır.

### Ekip rolleri

- Committer ve reviewer: [EddizEge](https://github.com/EddizEge)
- Signing approver: [EddizEge](https://github.com/EddizEge)

### Sürüm ve imzalama kuralları

- Yalnızca `EddizEge/CopyPaste` deposundaki kaynak ve build betiklerinden GitHub-hosted
  runner üzerinde üretilen CopyPaste dosyaları imzalanır.
- İmzalama yalnızca sürüm testleri başarıyla tamamlandıktan sonra istenir.
- Her gelecekteki imzalama isteği signing approver tarafından elle onaylanır.
- Başka projelere ait üçüncü taraf dosyalar CopyPaste sertifikasıyla imzalanmaz.
- Ürün sahibinin 23 Temmuz 2026 tarihli sürüm bazlı onaylarıyla yalnızca `v1.6.0`
  ve `v1.7.0` Setup ve portable ZIP dosyaları geçici olarak Authenticode imzasız yayımlanabilir.
  Bu dosyalar açıkça imzasız olarak belirtilir, SHA-256 değerleri yayımlanır ve
  imzasız MSIX dağıtılmaz.
- İmzalı yayın dosyalarının SHA-256 değerleri GitHub Release içindeki
  `SHA256SUMS.txt` dosyasında yayımlanır.
- İmza, Windows PowerShell'deki `Get-AuthenticodeSignature` veya Windows SDK
  `signtool verify /pa` komutuyla doğrulanabilir.

CopyPaste'in ağ ve veri işleme davranışı [gizlilik politikasında](PRIVACY.md) açıklanır.
Güvenlik sorunları herkese açık ayrıntılar paylaşılmadan depo sahibiyle iletişime
geçilerek bildirilmelidir.

## English

CopyPaste's SignPath Foundation open-source sponsorship application was declined on
July 24, 2026 because the project did not yet have sufficient external visibility and
community signals. SignPath signing is not currently active. The project may reapply
after gaining broader verifiable adoption; any paid service or certificate requires
separate product-owner approval. Store signing for an MSIX submitted to Microsoft Store
is planned separately from direct EXE/Setup Authenticode signing in the 1.8 roadmap.

### Team roles

- Committer and reviewer: [EddizEge](https://github.com/EddizEge)
- Signing approver: [EddizEge](https://github.com/EddizEge)

### Release and signing rules

- Only CopyPaste artifacts built on a GitHub-hosted runner from the source and build
  scripts in `EddizEge/CopyPaste` are signed.
- Signing is requested only after the release tests have passed.
- Every future signing request is manually approved by the signing approver.
- Third-party files from other projects are not signed with the CopyPaste certificate.
- With version-specific approvals from the product owner dated July 23, 2026, only
  the `v1.6.0` and `v1.7.0` Setup and portable ZIP may be published temporarily without an
  Authenticode signature. They are clearly identified as unsigned, SHA-256 hashes
  are published, and no unsigned MSIX is distributed.
- SHA-256 hashes for signed release assets are published in `SHA256SUMS.txt` within
  the GitHub Release.
- Signatures can be verified with `Get-AuthenticodeSignature` in Windows PowerShell
  or `signtool verify /pa` from the Windows SDK.

CopyPaste's network and data-handling behavior is described in the
[privacy policy](PRIVACY.md). Security issues should be reported privately to the
repository owner without disclosing sensitive details publicly.
