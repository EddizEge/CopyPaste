# CopyPaste Repository Guidance

## Product and communication

- CopyPaste is a Windows x64 desktop application that wraps the built-in Robocopy engine in a modern WinUI 3 interface. It targets .NET 8 and Windows 10 1809 or newer.
- The repository is `EddizEge/CopyPaste`; the current stable baseline is `v1.7.6` unless the working tree, tags, or GitHub release state proves otherwise.
- Speak with the product owner in Turkish unless they ask for another language. Keep technical names, commands, and code identifiers unchanged.
- User-visible application text must support both Turkish and English through the existing localization approach. Do not add hard-coded one-language UI strings.
- Keep the existing dark, compact, card-based visual language and purple accent palette. Preserve accessibility, keyboard use, scaling, and small-window behavior.
- Do not launch CopyPaste, show foreground windows, use Computer Use, trigger UAC, or otherwise interrupt the desktop unless the user explicitly permits visible UI testing in the current task. Prefer silent build and automated tests first.

## Repository map

- `src/CopyPaste.Core`: UI-independent models and services, including Robocopy command construction and execution, validation, comparison, verification, history, reports, queue recovery, updates, and scheduling data.
- `src/CopyPaste.App`: WinUI 3 application, windows, view models, localization, tray/startup behavior, notifications, task scheduling, protected-folder selection, Windows integration, and power management.
- `src/CopyPaste.ExplorerCommand`: Windows 11 `IExplorerCommand` integration used by the signed MSIX package.
- `tests/CopyPaste.Core.Tests`: executable Core regression/integration test suite.
- `installer`: Inno Setup definition for the per-user Setup EXE.
- `tools`: release, installer, MSIX, signing, and asset scripts.
- `website`: Turkish/English product and download site.
- `.github/workflows`: Pages and tagged-release automation.

Keep reusable business logic in `CopyPaste.Core`. UI code should coordinate services and render state, not duplicate command-building, validation, or transfer-result rules.

## Safety and compatibility invariants

- Copy operations must be non-destructive by default. Never introduce `/MIR`, `/PURGE`, `/MOVE`, `/MOV`, or equivalent deletion behavior into normal transfers. Any future synchronization deletion must be separately designed, previewed, opt-in, and recoverable.
- Keep the planned deletion utility isolated from copy, retry, recovery, scheduling,
  and synchronization flows. It may act only on targets the user explicitly selected
  for that deletion operation; no transfer completion, profile, schedule, or command-line
  argument may invoke it implicitly.
- Ordinary deletion must default to the Recycle Bin when the target and volume support
  it. Permanent deletion and best-effort secure erasure must be separate advanced modes
  that are disabled by default and require an exact target preview, prominent bilingual
  irreversible-action warnings, and a deliberate final confirmation. Never present any
  overwrite method as guaranteed unrecoverable on SSD/NVMe/TRIM media, flash storage,
  network/NAS shares, cloud-synced folders, snapshots, backups, journaled or copy-on-write
  file systems, or storage with remapped sectors.
- The deletion utility must refuse volume roots, Windows/system directories, the running
  application's files, and ambiguous or unresolved paths. It must not traverse junctions,
  symbolic links, mount points, or other reparse points outside the exact selected target.
  Never terminate locking processes, schedule boot-time deletion, wipe free space, format
  media, or clear snapshots automatically.
- Never take ownership of a source, rewrite ACLs, or run `takeown`/`icacls` to bypass access restrictions. Protected transfers must retain the one-UAC elevated workflow and use Robocopy backup semantics such as `/ZB` without altering the source permissions.
- Elevation in the deletion utility may address UAC-limited access only within the user's
  existing Windows authorization. It must not take ownership, rewrite ACLs, bypass OS
  protection, or claim it can delete an item that Windows still denies; report the exact
  locked, in-use, authorization, reparse-point, or protected-path reason instead.
- Preserve the allow-listed Robocopy argument builder. Use structured argument passing and correct Windows path quoting; never interpolate untrusted paths or free-form options into a shell command.
- Preserve Unicode paths, deep paths, large file counts, cancellation, restartable copies, and streamed output. Do not buffer an entire large Robocopy log in memory or block the UI thread during enumeration, hashing, network waits, or transfers.
- A Robocopy partial error is not a blanket transfer failure. Exit codes `8` through `15`, or parsed per-file failures, must remain `CompletedWithErrors`; show the failed paths and reasons and allow a focused retry. Exit code `16` or an unrecoverable launch/validation error is `Failed`.
- `CopyRootMode.SelectedFolder` is the user-facing default: selecting `C:\Source\Photos` with destination `D:\Backup` normally resolves to `D:\Backup\Photos`. `ContentsOnly` must remain an explicit alternative.
- Validate source/destination overlap, destination writability and free space, filters, exclusions, and unsafe path combinations before launch.
- Completion actions such as sleep or shutdown must run only after the required successful condition, only when explicitly enabled, and never from automated tests.
- Preserve settings and recovered queue compatibility when models evolve. Add safe defaults for newly introduced serialized fields.
- Updates must come from the configured GitHub Releases repository, prefer the Setup EXE, and verify the published SHA-256 before launch. Do not weaken verification or silently execute an unverified download.
- Do not add telemetry, upload paths/logs, expose credentials, or publish private user data.

## Development workflow

Before editing, inspect `git status`, the relevant code, tests, and current version/release state. Preserve unrelated user changes. Treat roadmap entries as plans, not proof that a feature is absent or authorization to publish it.

Use the repository-local .NET SDK when available:

```powershell
$env:DOTNET_ROOT = "$PWD\.dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet build CopyPaste.sln -c Release -p:Platform=x64
dotnet run --project tests/CopyPaste.Core.Tests/CopyPaste.Core.Tests.csproj -c Release
```

Run the application only for explicitly permitted visual or interactive QA:

```powershell
dotnet run --project src/CopyPaste.App/CopyPaste.App.csproj -c Release -p:Platform=x64
```

Release artifacts are built with:

```powershell
.\tools\Build-Release.ps1
.\tools\Build-Installer.ps1
.\tools\Build-Msix.ps1
```

- Add or update Core regression tests for command generation, path resolution, status classification, parsing, persistence, update verification, and other non-visual behavior.
- For transfer-engine changes, include safe temporary-directory tests with Unicode names, nested paths, repeated copies, and a controlled partial-failure case where practical.
- Keep asynchronous work cancellable and report progress without excessive dispatcher traffic.
- Handle Windows API, notification, registry, Task Scheduler, tray, network, and power-operation failures without crashing the main window.
- Do not make release tags, push branches, create GitHub releases, or replace the locally installed version unless the user explicitly asks for that action in the current task.
- Before every release, ask the product owner for explicit confirmation naming that
  specific version, even if a roadmap entry or an earlier broad approval exists.

## Versioning and releases

When preparing a requested release, update all applicable version surfaces consistently, including:

- `src/CopyPaste.App/CopyPaste.App.csproj`
- `src/CopyPaste.App/Package.appxmanifest`
- `installer/CopyPaste.iss` defaults
- `README.md` artifact names and release instructions
- `CHANGELOG.md`
- any visible version badge or website download metadata that actually contains the version

Every user-visible change must also be recorded in both `CHANGELOG.md` and the in-app bilingual
`ReleaseNotesCatalog`. Keep the Turkish and English change lists semantically equivalent. During
development, the upcoming entry remains marked as preview/in development; when a release is prepared,
mark that entry as released and create the next preview entry when new work begins. Version labels shown
by the application must come from assembly/package metadata rather than a manually maintained hard-coded
badge. Do not consider a release-note-related change complete unless the in-app notes remain readable in
both languages and the catalog has regression coverage for ordering, duplicate versions, and missing text.

Use a `vX.Y.Z` tag only after the Release build and tests pass. Tagged GitHub Actions must continue to produce the Setup EXE, portable ZIP, `SHA256SUMS.txt`, and the signed MSIX only when signing secrets are configured. Never claim artifacts are signed when no certificate was used.

Starting with `v1.7.5`, every release must include a supported self-contained Windows x64
portable ZIP alongside the Setup EXE and `SHA256SUMS.txt`. Extracting the ZIP and launching
`CopyPaste.App.exe` must require no installation or administrator rights. Portable and
installed artifacts must report the same version and share the same Core behavior, while
installer/package-only integrations such as uninstall registration and packaged Explorer
commands remain unavailable in portable mode. Release automation must fail rather than
silently publish a release when the portable artifact or its SHA-256 entry is missing.

`v1.6.0` and `v1.7.0` are product-owner-approved unsigned exceptions dated July 23,
2026, `v1.7.5` is a separate product-owner-approved unsigned exception dated
October 16, 2026, and `v1.7.6` is a separate product-owner-approved unsigned exception
dated October 19, 2026: for any of these versions publish only the Setup EXE, portable
ZIP, and `SHA256SUMS.txt`, prominently identify Setup/ZIP as Authenticode-unsigned, and
do not publish an unsigned MSIX. The workflow may allow this only when the tag is
exactly `v1.6.0`, `v1.7.0`, `v1.7.5`, or `v1.7.6` and the temporary repository variable
`ALLOW_UNSIGNED_RELEASE` is exactly `true`; remove the variable after the release
completes. These exceptions do not authorize any later release.

## Definition of done

A change is complete only when the checks proportional to its risk have passed:

1. The solution builds in Release/x64 without new warnings or errors.
2. The Core test executable passes, and relevant regression coverage was added.
3. Transfer changes were checked against real Robocopy behavior using disposable data; destructive and power actions were not exercised automatically.
4. UI changes received visual and interaction QA when the user allowed foreground testing, including Turkish and English, DPI/scaling, and window resizing as relevant.
5. Packaging changes produced and smoke-tested the intended artifact; updater/release changes also verified naming and SHA-256 behavior.
6. Documentation and changelog match the implemented behavior, and the final report distinguishes automated checks from anything still requiring a human/UAC test.

## Planned product direction

Keep this roadmap available across separate tasks, but re-check the code before implementing and do not combine releases or publish them without an explicit request.

### 1.6 — transfer planning and control

- Protected multi-folder selection with a usable checkbox/tree workflow.
- True Robocopy `/L` preview with copy, skip, overwrite, error, size, and time estimates.
- Clearer filter/exclusion editing and preview effects.
- Retry selected failed files, not only all failed files.
- Per-job speed limit and completion action.
- Show the exact resolved destination path before queueing and during the job.

### 1.7 — reliability and automation

- Stronger checkpoints and recovery after interruption or restart.
- Full schedule management: list, edit, pause/resume, run now, and delete.
- Optional USB-drive arrival trigger, idle/AC-power conditions, and clearer NAS/network resume state.
- Updater progress, restart timing, and a safe rollback path.

### 1.7.5 — controlled deletion and supported portable releases

- Add a separate, clearly destructive file and folder deletion utility; do not add delete
  switches to the normal Robocopy transfer engine and do not connect it to queued jobs,
  schedules, retries, synchronization, or completion actions.
- Provide three explicit modes: Recycle Bin deletion as the default; elevated permanent
  deletion for UAC-limited targets when the current Windows account is authorized; and
  opt-in best-effort secure file erasure that overwrites eligible regular-file content
  before deletion. Explain locked/in-use, authorization, protected-path, and unsupported
  storage failures without changing ownership or ACLs.
- Before any destructive action, show resolved paths, item and byte totals, mode, storage
  limitations, reparse-point handling, and whether recovery is expected to be possible.
  Permanent modes require a second confirmation containing an exact localized phrase;
  secure erasure must also make clear that copies, backups, snapshots, SSD wear-leveling,
  TRIM, and remote storage can make a universal recovery guarantee impossible.
- Secure erasure is limited to explicitly selected regular files on supported local volumes.
  Directory traversal must be bounded to the selected tree, must not follow reparse points,
  and must revalidate every target immediately before mutation. Free-space wiping is not
  part of 1.7.5 because it is volume-wide, slow, wear-intensive, and cannot provide the
  requested guarantee across modern storage.
- Stream progress and per-item results, support cancellation between files and overwrite
  passes, and report partially overwritten files honestly after cancellation or failure.
  Keep a local, privacy-preserving result summary without uploading paths or logs.
- Add regression coverage for path canonicalization, protected-root refusal, reparse-point
  boundaries, confirmation requirements, storage capability classification, cancellation,
  partial failures, and backward-compatible settings. Destructive integration tests may
  use only disposable temporary data created by the test itself.
- Formalize the existing no-install Windows x64 portable ZIP as a supported artifact for
  `v1.7.5` and every later release. Verify clean extraction and launch, version parity,
  SHA-256 publication, settings isolation expectations, update behavior, and clear notice
  for integrations unavailable without installation or MSIX identity.

### 1.8 — safe one-way synchronization and Microsoft Store distribution

- Visual source/destination diff for new, changed, missing, and conflicting items.
- Deletion disabled by default; any deletion requires a mandatory preview and recoverable destination such as the Recycle Bin.
- Reusable sync/backup profiles with understandable safety summaries.
- Prepare CopyPaste for Microsoft Store distribution as the preferred trusted MSIX
  channel: reserve the product in Partner Center, then use the exact Store-assigned
  package identity, publisher ID, and publisher display name rather than inventing
  or retaining development identities.
- Produce and validate a Store-ready x64 MSIX/MSIX upload package that preserves the
  native Windows 11 `IExplorerCommand` integration. Microsoft Store signing applies
  only after certification; local or CI artifacts must never be described as
  Store-signed.
- Prepare semantically equivalent Turkish and English Store listing text, privacy
  and support links, icons, screenshots, age/category answers, and release notes.
- Run Release/x64 build and Core tests, package validation, local test-certificate
  install/launch/uninstall/update checks, and final clean-profile Store package smoke
  tests before submission. Do not publish the Partner Center submission without
  explicit product-owner confirmation naming the specific version.
- Keep GitHub/web distribution separate: Setup EXE, portable ZIP, and
  `SHA256SUMS.txt` may continue only under the signing rules for that exact release.
  The approved `v1.6.0`/`v1.7.0`/`v1.7.5`/`v1.7.6` unsigned exceptions do not extend to `v1.8.0`;
  do not publish an unsigned MSIX outside Microsoft Store.

### Distribution work still outstanding

- SignPath Foundation open-source sponsorship application was submitted on July 23,
  2026 and declined on July 24, 2026 because the project did not yet have enough
  external public-trust and visibility signals. Reapply only after stronger,
  verifiable adoption such as stars, forks, contributors, release downloads,
  independent articles/videos/discussions, or institutional backing. A paid
  SignPath subscription requires separate product-owner approval.
- Complete the 1.8 Microsoft Store onboarding and Store-signed MSIX path for the
  native Windows 11 context menu. Partner Center account creation, identity
  verification, agreements, and any sensitive authentication steps remain
  product-owner actions.
- Authenticode certificate and trustworthy signing for production EXE/installer
  artifacts distributed directly from GitHub or the website remains separate from
  Microsoft Store signing.
- Final installer/update smoke tests on a clean Windows user profile and a representative managed/company PC.

## Code review rules

- Flag any path that can delete or modify source data, weaken update verification, bypass the Robocopy allow-list, alter ACL/ownership, misclassify partial success, perform power actions unexpectedly, or block the UI thread.
- Require regression coverage for changes to command arguments, exit-code parsing, destination resolution, failed-item parsing/retry, settings migration, schedules, and updater asset selection.
- Treat misleading success/failure messages, untranslated visible strings, and a release whose displayed/downloaded version disagrees with its binaries as correctness defects.
