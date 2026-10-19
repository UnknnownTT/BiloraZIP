# BiloraZIP v1.0 RC1 — Folder navigation / Drag & Drop hotfix

## Алдааны шалтгаан

`Cannot copy a folder into itself.` нь архив задлахтай холбоогүй. Хавтас руу хоёр дарах үед хулганын багахан хөдөлгөөнийг `EntriesList_PreviewMouseMove` drag гэж андуурч, BiloraZIP-ийн өөрийн FileDrop-г буцаагаад өөр дээрээ Drop хийх боломжтой байв.

## Засвар

1. `Window_DragOver` / `Window_Drop`: `_outboundDragging` үед өөрийн цонхонд буцаж Drop хийхийг хориглов.
2. `EntriesList_PreviewMouseLeftButtonDown`: хоёр дахь даралтад drag эхлүүлэхгүй.
3. `EntriesList_MouseDoubleClick`: бодитоор хоёр дарсан мөрийг сонгож, өмнө сонгосон мөр рүү андуурч орохгүй.
4. `CopyDroppedPaths`: эх сурвалж ба зорилтот зам ижил бол алдаа гаргахгүй алгасана. Харин хавтсыг өөрийн дэд хавтас руу хуулахыг хориглосон хэвээр.
5. `FileDropSafety` болон regression self-tests нэмсэн.

## Турших

- Энгийн хавтас дээр олон удаа хурдан double-click хийх; Diagnostic алдаа гарах ёсгүй.
- Өөр хавтаснаас файлыг drag & drop хийж хуулах.
- Нэг хавтас дотор өөрийнх нь файлыг буцааж drop хийхэд файл дарж бичихгүй, алдаа гаргахгүй.
- Ctrl+C / Ctrl+V ажиллагаа хэвээр.

## Build

`./run-selftests.ps1` дараа нь `./build-installer.ps1`.

Windows/.NET GUI тест энэ орчинд хийгдээгүй; эх кодын засварыг Windows дээр баталгаажуулах шаардлагатай.
