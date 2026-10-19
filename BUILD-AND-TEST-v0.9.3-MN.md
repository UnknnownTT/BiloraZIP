# BiloraZIP v0.9.3 — BILZ & Single Instance (туршилтын хувилбар)

Энэ багц нь Windows дээр **Compile/Runtime тест хийгдээгүй хөгжүүлэлтийн эх код**. Python BARM/BILZ reference тест 8/8 амжилттай; эдгээр нь Windows WPF UI эсвэл installer-ийн туршилт биш.

## Гол өөрчлөлтүүд

- **Нэг GUI процесс / нэг tray icon:** Нэг Windows хэрэглэгчийн session-д `Local\BiloraZIP_Main_093_*` mutex болон current-user-only named pipe ашиглан давхар эхлүүлэлтүүдийг өмнөх BiloraZIP цонхонд дамжуулна.
- **BILZ-1:** Туршилтын BARM-1 engine-ийг `src/BiloraZIP/Codec/` хавтсанд нэмсэн. `Archive format -> BILZ (experimental)` сонголттой. Create, Open, List, Test, Extract, Extract Selected болон preview холбогдсон.
- **Windows Explorer контекст цэс:** Нийт файл-хавтасны `AllFilesystemObjects\shell\BiloraZIP.Compress` бүртгэл, нэг файл, олон файл, файл+хавтас зэрэг сонголтод ижил команд. Classic right-click цэсэнд (Windows 11 бол Show more options) ашиглана. Installer-ийн `Explorer right-click commands` сонголтыг идэвхжүүл.
- **Windows integration:** `Options -> Windows integration` дотроос `BILZ` association болон per-user classic menu-ийг тохируулж болно.
- v0.9.2-ийн Copy/Cut/Paste/Rename/Delete, UI, Marquee selection, Drag & Drop болон ZIP/RAR/7Z кодын суурийг хадгалсан; энэ хувилбарт эдгээрийг тус бүрээр Windows дээр regression test хийгээгүй.

## Build

**Чухал:** Суулгахаас өмнө хуучин v0.9.2-ийн бүх System Tray icon-оос `Exit` хий. Шаардлагатай бол Task Manager дээр үлдсэн `BiloraZIP.exe`-үүдийг идэвхтэй архивлах ажиллагаа байхгүй үед хаа. Хуучин процесс шинэ Single Instance mutex-ийг мэдэхгүй.

ZIP задлаад `BiloraZIP` хавтсанд PowerShell нээнэ.

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

Installer гарвал `installer\output\BiloraZIP-Setup-0.9.3-win-x64.exe` файлыг суулгана. `Explorer right-click commands (legacy menu)`-ийг сонго.

## Windows дээр хийх шалгалтууд

1. BiloraZIP нээ. `Exit -> Minimize to tray` сонго. Desktop shortcut-ийг 3 удаа дар. Нэг цонх сэргэж, **нэг tray icon** үлдэх ёстой. Task Manager-д үндсэн нэг процесс байх ёстой (launcher-ийн богино настай helper процесс түр харагдаж болно).
2. Tray icon дээр давхар дарахад өмнөх цонх сэргэх, `Exit` хийхэд tray icon арилахыг шалга.
3. BiloraZIP дотроос хэд хэдэн жижиг файл/хавтас сонгоод **BILZ (experimental)** үүсгэ. `Fastest` болон `Normal / Optimal` сонголтуудыг турш. `Test archive after creating` ажиллах ёстой.
4. Шинэ `.bilz` нээж файл жагсаах, дэд хавтас руу нэвтрэх, задлах, ганц/олон сонгосон файлыг задлах, BILZ-ийн Integrity Test, Unicode/Mонгол нэртэй файлын агуулгын hash-ийг шалга.
5. ZIP, RAR, 7Z нээх/задлах, нууц үгтэй ZIP, Explorer navigation болон keyboard shortcut-уудыг дахин шалга.
6. Windows Explorer дээр 1 файл, 1 хавтас, 3 файл + 2 хавтас сонгоод **Show more options -> Compress with BiloraZIP...** ажиллах эсэхийг шалга. Нэг л archive options цонх гарна. Windows-ийн `Document` context model нь 15-аас олон объектын сонголт дээр цэсийг нууж болно. 15-аас олон сонголтыг BiloraZIP дотроос хийж болно.
7. Context menu харагдахгүй бол BiloraZIP -> Options -> Windows Integration: `Show BiloraZIP in the classic right-click menu` идэвхжүүлж, `Apply for this user` дараад Explorer-г дахин нээ. Installer-ийн legacy context-menu тохиргоог мөн шалга.

## BILZ-1-ийн одоогийн хязгаарлалт

- **Experimental:** Өдөр тутмын цорын ганц нөөц архивын форматаар ашиглаж болохгүй, эх файлуудаа хадгал.
- Нууц үг/AES, RAR үүсгэх, solid compression, multi-volume байхгүй.
- Нэг файл 1 GiB, нийт өгөгдөл 4 GiB, 100 000 entry хүртэлх прототип хамгаалалттай.
- Одоогийн BARM encoder урсгал шахалт явуулж байх үед cancel шууд зогсохгүй; блок/архив дуусмагц хүсэлтийг боловсруулна. Progress-д завсрын byte мэдээ гаргахгүй.
- BILZ selective extract нь эхлээд бүх архивыг түр хавтсанд бүрэн шалгадаг, дараа нь сонгосон файлуудыг зөөдөг. Том архивт хугацаа, нэмэлт дискний зай зарцуулна.
- Windows 11 **шинэ** context menu-ийн native shell extension байхгүй; зөвхөн legacy (Show more options) цэс.
- Аль хэдийн шахсан зураг, видео дээр `.bilz` хэмжээ багасахгүй байж болно. RAR/7Z-ээс илүү шахдаг нь батлагдаагүй.
- Бодит Windows Build, Inno Setup compiler, drag/drop, Shell registration, tray-ийн tests-г энэ орчинд хийх боломжгүй.

Форматын дэлгэрэнгүй тодорхойлолт `spec/BARM-1.md`, `spec/BILZ-1.md` дотор бий.
