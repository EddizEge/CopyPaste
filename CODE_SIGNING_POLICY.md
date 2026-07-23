# Code signing policy / Kod imzalama politikası

## Türkçe

CopyPaste, SignPath Foundation'ın açık kaynak sponsorluğuna başvurmaktadır. Başvuru
onaylandıktan sonra üretim sürümleri şu modelle imzalanacaktır:

> Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
> [SignPath Foundation](https://signpath.org/).

### Ekip rolleri

- Committer ve reviewer: [EddizEge](https://github.com/EddizEge)
- Signing approver: [EddizEge](https://github.com/EddizEge)

### Sürüm ve imzalama kuralları

- Yalnızca `EddizEge/CopyPaste` deposundaki kaynak ve build betiklerinden GitHub-hosted
  runner üzerinde üretilen CopyPaste dosyaları imzalanır.
- İmzalama yalnızca sürüm testleri başarıyla tamamlandıktan sonra istenir.
- Her SignPath imzalama isteği signing approver tarafından elle onaylanır.
- Başka projelere ait üçüncü taraf dosyalar CopyPaste sertifikasıyla imzalanmaz.
- Ürün sahibinin 23 Temmuz 2026 tarihli sürüm bazlı onayıyla yalnızca `v1.6.0`
  Setup ve portable ZIP dosyaları geçici olarak Authenticode imzasız yayımlanabilir.
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

CopyPaste is applying for SignPath Foundation's open-source sponsorship. After
approval, production releases will use the following model:

> Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
> [SignPath Foundation](https://signpath.org/).

### Team roles

- Committer and reviewer: [EddizEge](https://github.com/EddizEge)
- Signing approver: [EddizEge](https://github.com/EddizEge)

### Release and signing rules

- Only CopyPaste artifacts built on a GitHub-hosted runner from the source and build
  scripts in `EddizEge/CopyPaste` are signed.
- Signing is requested only after the release tests have passed.
- Every SignPath signing request is manually approved by the signing approver.
- Third-party files from other projects are not signed with the CopyPaste certificate.
- With version-specific approval from the product owner dated July 23, 2026, only
  the `v1.6.0` Setup and portable ZIP may be published temporarily without an
  Authenticode signature. They are clearly identified as unsigned, SHA-256 hashes
  are published, and no unsigned MSIX is distributed.
- SHA-256 hashes for signed release assets are published in `SHA256SUMS.txt` within
  the GitHub Release.
- Signatures can be verified with `Get-AuthenticodeSignature` in Windows PowerShell
  or `signtool verify /pa` from the Windows SDK.

CopyPaste's network and data-handling behavior is described in the
[privacy policy](PRIVACY.md). Security issues should be reported privately to the
repository owner without disclosing sensitive details publicly.
