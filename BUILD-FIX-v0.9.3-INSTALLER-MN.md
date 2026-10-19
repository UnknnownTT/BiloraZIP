# BiloraZIP v0.9.3 — Installer нэрийн засвар

## Алдааны шалтгаан

`installer/BiloraZIP.iss` нь `BiloraZIP-Setup-0.9.3-win-x64.exe` үүсгэж байсан боловч `build-installer.ps1` нь `0.9.2` нэртэй файлыг шалгаж байсан. Программ болон Inno Setup compile амжилттай дууссан ч скрипт алдаа заасан.

## Засвар

- `build-installer.ps1` дээрх хатуу `0.9.2` нэрийг арилгасан.
- `MyAppVersion`, `OutputBaseFilename`, `OutputDir`-г Inno Setup (`.iss`) файлаас автоматаар уншдаг болсон.
- Өмнөх compile-аас үлдсэн installer-г шинэ compile-тай андуурахаас сэргийлж зөвхөн зорилтот installer-г compile-аас өмнө устгана.
- `.BILZ`, single-instance tray, Right-click болон бусад эх код өөрчлөгдөөгүй.

## Windows дээр ажиллуулах

Шинэ ZIP-ийг тусдаа хавтсанд задлаад `BiloraZIP` дотор:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

Амжилттай бол `installer\output\BiloraZIP-Setup-0.9.3-win-x64.exe` гарна. Хуучин ZIP-ээр өмнө нь үүссэн 0.9.3 installer файл бас байж болох ч засварласан скриптээр дахин үүсгэж шалгах боломжтой.

> Энэ багцыг Windows .NET SDK / Inno Setup дээр бодитоор ажиллуулж шалгаагүй; зөвхөн багцын бүтцийг статик шалгасан.
