# Release validation / Validasi rilis

Created by Ariana Mischa Fadhila, Syubadubin Studios.

## Reproducible package

```powershell
python tools/audit-assets.py
powershell -NoProfile -File tools/publish.ps1 -Runtime win-x64
powershell -NoProfile -File tools/verify-package.ps1 -Package artifacts/packages/<package-directory>
```

The script publishes self-contained desktop and server binaries, assets and third-party notices, creates a SHA-256 manifest and ZIP, and leaves previous packages intact. Install from the extracted directory with `powershell -NoProfile -File install.ps1`. Installation defaults to `%LOCALAPPDATA%/Programs/SyubadubinStudios/NusantaraFishingMadness`, creates a Start Menu shortcut and registers current-user uninstall. No administrator access is needed. Close the game before uninstalling/upgrading.

`uninstall.ps1` removes only manifest-recorded application files. User saves live separately under `%LOCALAPPDATA%/SyubadubinStudios/NusantaraFishingMadness` and are preserved. The verifier installs into a unique temporary directory without shell/registry integration and checks unknown-file preservation. Checksums detect accidental modification; authenticity requires a trusted signature.

`publish.ps1 -Runtime linux-x64`, `osx-arm64` or `osx-x64` builds corresponding portable packages. These are packaging targets until tested on their operating systems. macOS app bundles, notarization and a Linux desktop integration installer remain release work.

Cross-published Linux x64 and macOS ARM64 portable packages were generated locally. When extracting a Windows-created ZIP on Unix, restore executable permissions with `chmod +x NusantaraFishing.Desktop server/NusantaraFishing.Server` before launching from a terminal. This is not a substitute for OS-specific installer validation.

## Signing

Windows builds are unsigned unless a real trusted code-signing certificate is supplied:

```powershell
powershell -NoProfile -File tools/publish.ps1 -CertificateThumbprint <installed-certificate-thumbprint>
```

The hook uses Windows SDK signtool, SHA-256 and RFC3161 timestamping, then verifies both first-party executables. Public release must verify the certificate chain on a clean target system. No signing certificate has been supplied. This repository does not fabricate a trusted identity or claim unsigned packages are signed.

## Validation status

- [x] Windows Release build and deterministic gameplay/save/network tests.
- [x] Four HTTP clients with 100 ms injected delay: shared boss, reconnect, restart and one reward per participant.
- [x] Native four-player scene, keyboard and virtual controller input.
- [x] Fourteen-region GPU tour and morning/noon/sunset/night captures.
- [x] Original audio mixing, English settings screen and water/environment GPU smoke.
- [x] Single-adapter performance captures and asset/dependency inventory.
- [ ] Linux and macOS native rendering, audio, input and packaged-host runtime tests.
- [ ] Broader hardware, physical controllers, long sessions and independent balance/usability playtests.
- [ ] Trusted Windows signing; macOS bundle signing/notarization; clean-machine installation.
- [ ] Owner confirmation of Rodin redistribution terms and repository-wide license selection.

GitHub Actions runs build, unit and HTTP acceptance checks on Windows/Linux/macOS when this repository is pushed to an enabled GitHub project. A separate manual workflow builds unsigned preview ZIPs on each OS. The workflows being present are not proof those runners have executed. GPU validation requires a real graphics session and is deliberately not reported as covered by headless CI.

**ID:** Paket lokal siap diperiksa, tetapi rilis publik belum dinyatakan selesai. Sertifikat, mesin uji OS lain, dan persetujuan hak distribusi dari pemilik diperlukan untuk menyelesaikan checklist eksternal. Hasil uji otomatis tidak menggantikan playtest manusia.
