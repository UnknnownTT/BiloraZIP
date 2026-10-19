# BiloraZIP v1.0 RC1 — Explorer Background Operations Hotfix

## Зорилго
Windows Explorer дээр хулганы баруун товч (Right-click) ашиглан архив үүсгэх,
задлах үед BiloraZIP-ийн үндсэн том цонх томорч харагдахгүй.

## Хийгдсэн засвар
- App.xaml.cs: --compress, --extract-to, --extract-here, --extract-subfolder
  параметрээр эхэлсэн шинэ process-ийн үндсэн цонхыг **minimized** төлөвтэй нээнэ.
  Энгийн --open, Start Menu/desktop нээлт хэвийн байна.
- MainWindow.xaml.cs: өмнө ажиллаж байсан процесс руу Explorer командаар
  хүсэлт ирэхэд RestoreFromTray() / Activate() дуудахгүй.
- Архивын сонголт, задлах тохиргоо, нууц үг, явцын цонхнуудыг
  minimized/hidden үндсэн цонхны Owner болгохгүй; тусдаа taskbar цонх
  болгон харуулна. Дэлгэцийн төвд гарна.
- --extract-to: async void click handler-ийн ажиллагааг дутуу хүлээлгүй
  ExtractWithOptionsAsync() ажиллагааг бүтнээр нь await хийнэ.
- BiloraDialog: minimized үндсэн цонхыг modal owner гэж сонгохгүй;
  owner байхгүй бол ShowInTaskbar=true.
- DiagnosticsWindow: алдааны цонх main window minimized үед тусдаа гарна.
- Installer-ийн хувилбарын нэрийг ялгаж шинэчилсэн.

## Windows дээр гараар шалгах
1. BiloraZIP-ийг бүрэн хаагаад Explorer дээр 1 ZIP сонгож
   Right-click > Show more options > BiloraZIP > Extract files...
   Сонголтын цонх гарч, үндсэн цонх minimized хэвээр байх ёстой.
2. ZIP дээр Extract here; зөвхөн Progress цонх гарч,
   үндсэн цонх minimized хэвээр байх ёстой.
3. Файл/хавтас сонгоод Compress with BiloraZIP;
   Archive Options болон Progress тус тус харагдах ёстой.
4. BiloraZIP-ийг tray руу Hide хийгээд 1–3 алхмыг давт.
   Tray-аас үндсэн цонх өөрөө сэргээгдэх ёсгүй.
5. BiloraZIP-ийг ердийнхөөрөө Start Menu-ээс нээхэд
   үндсэн Explorer цонх томоор гарч ирэх ёстой.
6. Нууц үгтэй ZIP задлахад password prompt нээлттэй харагдах;
   алдаа гарвал Diagnostics цонх харагдах ёстой.
7. Дээрх үйлдлүүдийг үндсэн цонх анхнаасаа харагдаж байгаа
   үед давт: хэрэглэгчийн өмнөх цонхны төлөвийг хүчээр өөрчлөхгүй.

## Build
PowerShell (Windows, .NET 10 SDK + Inno Setup):

    .\run-selftests.ps1
    .\build-installer.ps1

Expected installer: BiloraZIP-Setup-1.0.0-rc1-shellui1-win-x64.exe

## Хязгаарлалт
Энэ орчинд Windows GUI болон .NET build-ийг бодитоор туршаагүй.
Installer EXE биш, build хийх эх кодын багц хүргэж байна.
