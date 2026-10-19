# BiloraZIP — v1.0.0 Release Candidate 1

**Free, open-source Windows archive manager — Made in Mongolia 🇲🇳**

This repository contains a Windows .NET 10 WPF application, source code, archive/security self-tests, Inno Setup installer builder and portable build script. It is **not a verified stable v1.0 release** until the Windows build, tests and manual acceptance checklist pass.

## Features

- Open/extract: ZIP, RAR, 7Z, TAR, ZIPX, XAPK, BILZ, TAR.GZ (codec support varies).
- Create: ZIP (optionally AES-256), TAR, TAR.GZ and experimental BILZ.
- Experimental split BILZ `.bilz.001` etc. with per-part and whole-archive SHA-256 checks.
- Explorer-style browsing with **explicit mouse-wheel scrolling**, marquee selection, drag/drop, file operations, right-click menu.
- ISO mount/unmount using Windows built-in read-only DiskImage commands (never autoruns contents).
- Mongolian/English UI, settings, diagnostics, donation support (owner-configured HTTPS link).

## Build on Windows

Requires .NET 10 SDK, Windows 10/11 x64, and Inno Setup 6/7 for the installer.

```powershell
.\run-selftests.ps1
.\build-installer.ps1
.\build-portable.ps1
```

Expected build outputs after successful execution:
- `installer/output/BiloraZIP-Setup-1.0.0-rc1-win-x64.exe`
- `release/BiloraZIP-1.0.0-rc1-portable-win-x64.zip`

These executables are **not bundled in the source ZIP**. The Windows GitHub Actions workflow can build and upload them when connected to a repository.

## Release checklist

See [V1-RELEASE-CHECKLIST-MN.md](V1-RELEASE-CHECKLIST-MN.md). Third-party license audit is pending: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Security notes

- Archives are untrusted input; output paths are checked and file data is staged before publishing each file.
- Extraction is **not** all-or-nothing: already completed files may remain after a later error or cancellation.
- Overwrite requires explicit selection. Existing files are never replaced by default.
- Password-protected ZIP file payloads use AES-256; ZIP file names remain visible. ZIP archives from other tools can contain mixed encrypted/plain entries.
- BILZ/BARM-1 is **experimental**, no encryption, codec limits are currently 1 GiB/file and 4 GiB total, and volume reassembly needs extra temporary disk space.
- `Support BiloraZIP` is voluntary and no payment is processed by the application.
