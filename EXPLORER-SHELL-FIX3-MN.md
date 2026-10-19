# BiloraZIP v0.9.3 — Shell Fix 3 (Installer-ээс автоматаар ажиллах засвар)

## Юунаас болсон бэ?

`installer/BiloraZIP.iss` файл дахь `contextmenu` task нь `Flags: checkedonce` гэсэн тохиргоотой байв. Inno Setup-ийн дүрмээр өмнөх хувилбар суусан үед энэ тохиргоо анхнаасаа идэвхгүй байдаг. Тиймээс шинэ installer үүсгэсний дараа ч compression right-click бүртгэл хийгдэхгүй байж болно.

Тусдаа `Repair-BiloraZIP-ExplorerMenu.ps1` нь одоогийн хэрэглэгчийн `HKCU` бүртгэлд ZIP/BILZ үүсгэх командыг амжилттай оруулж байсан. Харин installer анхнаасаа `HKLM`-д бүртгэж байв.

## Ямар засвар орсон бэ?

1. `contextmenu` task-ийг `explorermenu` гэж **шинээр нэрлэсэн**, `checkedonce`-ийг устгасан. Өмнөх хувилбарын санагдсан идэвхгүй тохиргоо шинэ task-д нөлөөлөхгүй. Энэ сонголт анхнаасаа идэвхтэй (хэрэглэгч хүсвэл унтраана).
2. Шинэ installer-д амжилттай туршигдсан **яг тэр Repair скриптийг** `{app}` рүү хуулах болсон.
3. Installer суулгаж дууссаны дараа `runasoriginaluser` эрхээр скриптийг `-ExePath`-тай ажиллуулж, одоогийн хэрэглэгчийн `HKCU` бүртгэлийг нөхөж өгнө. `Send to` shortcut мөн энэ аргаар үүснэ.
4. Машины түвшний хуучин `HKLM` задлах/архивлах бүртгэлүүд хэвээр; бусад архивын программын бүртгэлийг өөрчлөхгүй.
5. Uninstall хийхэд боломжтой тохиолдолд тухайн хэрэглэгчийн BiloraZIP right-click бүртгэлийг цэвэрлэнэ.

## Windows дээр суулгах

1. Хуучин ажиллаж байгаа BiloraZIP-ээ бүрэн хаана (tray-д нуугдсан процессыг мөн).
2. ZIP-г тусдаа хавтсанд задлаад PowerShell дээр:

```powershell
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

3. `installer\output\BiloraZIP-Setup-0.9.3-shellfix3-win-x64.exe`-ийг **энгийн хоёр даралтаар** ажиллуулна. `Run as administrator` гэж тусгайлан эхлүүлэх хэрэггүй (шаардлагатай бол UAC өөрөө зөвшөөрөл асууна).
4. Additional Tasks дотроос **Explorer right-click commands (ZIP/BILZ creation and archive extraction)** чагттай байгааг шалгана.
5. Суулгаж дууссаны дараа File Explorer > нэг файл > Right-click > Show more options, дараа нь нэг хавтас болон олон холимог файл дээр `Compress with BiloraZIP (ZIP / BILZ)...` харагдаж байгааг шалгана.

## Шалгалтын туслах команд

```powershell
reg query "HKCU\Software\Classes\*\shell\BiloraZIP.Compress\command" /ve
reg query "HKCU\Software\Classes\Directory\shell\BiloraZIP.Compress\command" /ve
```

Хэрэв Windows Explorer цэсээ кэшэлсэн бол Task Manager > Windows Explorer > Restart.

**Хязгаарлалт:** Inno Setup-г эхнээс нь **Run as administrator** гэж нээсэн, эсвэл өөр администраторын нууц үг оруулж өндөр эрх авсан тохиолдолд `runasoriginaluser` энгийн хэрэглэгч рүү буцаж ажиллах баталгаа байхгүй. Энэ тохиолдолд суулгасан дараа BiloraZIP-ийн үндсэн хэрэглэгчийн PowerShell-ээс `Repair-BiloraZIP-ExplorerMenu.ps1`-ийг нэг удаа ажиллуулж болно. `-Remove` нь зөвхөн тухайн хэрэглэгчийн BiloraZIP-тай холбоотой HKCU бүртгэлийг цэвэрлэнэ.

Энэ нь Windows 11-ийн **Show more options** сонгодог цэсийн интеграц; Windows 11-ийн шинэ үндсэн цэсэнд native shell extension нэмээгүй. `.rar` файл үүсгэх хөдөлгүүр одоохондоо байхгүй.

## Windows дээр бодит тест

Энэ орчинд Inno Setup болон Windows WPF compiler байхгүй тул installer ажиллахыг Windows дээр баталгаажуулах шаардлагатай. Шинэчлэлт зөвхөн installer скрипт ба баримт бичигт орсон, C# эх код өөрчлөгдөөгүй.
