# BiloraZIP v0.9.5 Preview — Монгол тайлбар

Энэ нь **хөгжүүлэлтийн туршилтын эх код**. Windows дээр build болон runtime self-tests-ийг энэ орчноос ажиллуулаагүй. Одоогийн ажиллаж байгаа BiloraZIP хувилбараа шууд устгалгүй, тусад нь туршаарай.

## Хийгдсэн өөрчлөлт

1. **More цэсний icon:** `App.xaml`-ийн custom `MenuItem` template өмнө нь `Icon`-ийг огт зурдаггүй байсныг зассан. `More` цэсний дүрсийг тогтвортой `Icon_*` resource-аас авдаг болгосон; theme солиход шинэчилнэ.
2. **Diagnostics:** Алдааны мөр сонгоход доод талд `TextBox`-д бүрэн мэдээлэл харагдана. Хулганаар хэсэгчлэн сонгох, Ctrl+C ашиглах боломжтой. Хүснэгтийн хөндлөн scroll хэвээр.
3. **Extraction options:** Задлах байршил, `Fail` (анхдагч), `Skip`, `Rename`, `Overwrite` (баталгаажуулалттай), задласны дараа Explorer нээх сонголтууд нэмсэн. Байгаа хавтсыг дахин ашиглана. ZIP/RAR/7Z/TAR болон BILZ-д үйлчилнэ. Архивын аюулгүй байдлын шалгалт хэвээр.
4. **Архив үүсгэх формат:** `TAR.GZ` (gzip-тай TAR) нэмсэн. ZIP/TAR/BILZ хэвээр. **RAR/7Z бичих боломж нэмэгдээгүй** — proprietary эсвэл тусгай encoder шаардлагатай. TAR.GZ шифрлэлгүй.
5. **BILZ олон хэсэг:** `.bilz.001`, `.002` гэх мэт үүсгэх туршилтын механизм нэмсэн (100/700/1024/4096 MiB). Эхний `.001`-ийг нээхэд бүх хэсгийн толгой, хэмжээ, SHA-256-ийг шалгаж түр файлд нэгтгээд BILZ-1 хөдөлгүүрээр уншина. Дутуу/гэмтсэн хэсгийг хориглоно. Нэгтгэхэд **сул дискний зай** шаардлагатай. Энэ бол шинэ туршилтын volume envelope, стандарт RAR/7Z multipart биш. Бүх хэсэг нэг хавтсанд байх ёстой. **ISO/EXE setup автоматаар ажиллуулахгүй.**
6. **ISO Mount/Unmount:** Tools цэс болон ISO файл дээрх BiloraZIP context menu-д `Mount ISO image (read-only)…`, `Unmount ISO image…` нэмсэн. Windows-ийн `Mount-DiskImage`/`Dismount-DiskImage` ашиглана. Windows PowerShell болон тухайн системийн эрх/virtual disk дэмжлэг шаардлагатай. ISO доторх `.exe` автоматаар ажиллахгүй.

## Build & tests (Windows)

.NET 10 SDK, Inno Setup 6 эсвэл 7 суулгасан Windows компьютер дээр:

```powershell
.\run-selftests.ps1
.\build-installer.ps1
```

Installer: `installer\output\BiloraZIP-Setup-0.9.5-preview-win-x64.exe`

Self-tests-д ZIP AES-256, mixed-encryption, ZIP Slip, conflict policy, TAR.GZ roundtrip, BILZ split/reassemble, missing part, corrupt part шалгах нэмэлт тестүүд орсон.

## Анхааруулга

- Шинэ BILZ part форматыг тогтвортой стандарт гэж үзэхгүй; архивын цорын ганц хуулбарыг энэ туршилтын форматаар хадгалахгүй.
- Өгөгдлөө өөр газар нөөцлөөд `Overwrite` сонголтыг турш.
- Одоогийн v0.9.4-ийг автоматаар шинэчлэхээс өмнө self-tests болон Windows Explorer UI-г шалга.
- ISO нь програм суулгагчийг автоматаар ажиллуулах боломж биш; зөвхөн виртуал диск холбоно.
