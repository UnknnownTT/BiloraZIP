# BiloraZIP v0.9.3 — Windows Explorer Right-click засвар 2

Энэ нь **ZIP/BILZ үүсгэх команд Windows Explorer-ийн хуучин Right-click цэсэнд харагдахгүй** асуудлын засварын багц юм.

## Шууд засах (шинэ installer бүтээх шаардлагагүй)

1. BiloraZIP v0.9.3 суусан байх ёстой.
2. ZIP багц доторх `BiloraZIP` хавтсанд PowerShell нээ.
3. Ажиллуул:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-BiloraZIP-ExplorerMenu.ps1
```

Хэрэв `BiloraZIP.exe was not found` гарвал:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-BiloraZIP-ExplorerMenu.ps1 -ExePath 'C:\Program Files\BiloraZIP\BiloraZIP.exe'
```

Энэ нь зөвхөн **HKCU\Software\Classes** доторх BiloraZIP-ийн гурван архив үүсгэх цэсийн бүртгэлийг өөрчилнө. Бусад программын товч цэсний бүртгэлд хүрэхгүй. `Send to` хавтсанд нөөц shortcut нэмнэ.

## Шалгах

Windows Explorer -> нэг файл -> Right-click -> **Show more options** -> `Compress with BiloraZIP (ZIP / BILZ)...`.

Дараа нь нэг хавтас, мөн олон файл ба хавтас хамтад нь сонгож турш. Нээгдэх BiloraZIP цонхны `Archive format`-аас **ZIP** эсвэл **BILZ** сонго.

**RAR файл үүсгэх хөдөлгүүр байхгүй.** Одоогоор RAR-ийг унших/задлах л дэмжинэ. Туршилтанд RAR үүснэ гэж амлахгүй.

Сонголт хэт олон, эсвэл тухайн Windows хувилбарт Shell цэс шүүгдвэл **Show more options -> Send to -> BiloraZIP - Create archive** ашигла. Сонгодог `Document` shell verb нь 15-аас дээш сонголтод цэсээ нууж болно.

Хэрэв цэс шууд харагдахгүй бол Windows Explorer-ийг Task Manager -> Windows Explorer -> Restart гэдгээр дахин эхлүүл; хэзээ ч бусад програмын registry key-ийг устгаж болохгүй.

Энэ нь Windows 11-ийн **шинэ үндсэн** context menu-ийн COM / IExplorerCommand интеграц биш. Зөвхөн `Show more options` доторх сонгодог цэсийг засах зорилготой.

## Буцаах

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-BiloraZIP-ExplorerMenu.ps1 -Remove
```

## Дараагийн installer-д засварыг хадгалах

`IntegrationService.cs` болон `BiloraZIP.iss` дахь shell verb бүртгэлийг шинэчилсэн. Эдгээр өөрчлөлт Windows дээр compile хийгдээгүй тул бодит туршилтын дараа шинээр installer үүсгэнэ.
