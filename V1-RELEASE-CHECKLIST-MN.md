# BiloraZIP v1.0 — Release Candidate 1 шалгах хуудас

## 1. Автомат тест (Windows 10/11 x64)
- [ ] `run-selftests.ps1` бүх тест PASS.
- [ ] `build-installer.ps1` амжилттай, setup EXE үүссэн.
- [ ] `build-portable.ps1` амжилттай, portable ZIP үүссэн.
- [ ] GitHub Actions Windows build амжилттай.

## 2. Explorer ба хулгана
- [ ] Downloads хавтсанд 100+ файлтай үед mouse wheel дээш/доош гүйлгэнэ.
- [ ] Mouse wheel: mouse cursor файл, хавтас, хоосон талбар дээр байхад ажиллана.
- [ ] Settings → Explorer mouse-wheel scroll lines 1 / 3 / 10 өөрчлөгдөнө.
- [ ] Marquee selection, Ctrl/Shift selection, drag-and-drop, keyboard arrows ажиллана.
- [ ] ZIP/RAR/BILZ нээхэд scroll хэвийн.

## 3. Архив ба аюулгүй байдал
- [ ] ZIP, ZIP AES-256, RAR, 7Z, TAR, TAR.GZ, BILZ бодит файлуудыг уншиж/задална.
- [ ] Хольж шифрлэсэн ZIP-ийн хамгаалалтгүй файлуудыг анхааруулгатай харуулна.
- [ ] Нууц үг буруу, эвдэрсэн архив, ZIP Slip, reserved Windows name, decompression bomb тест.
- [ ] Файл давхцах үед Stop/Skip/Rename/Overwrite; зөвшөөрөлгүй дарж бичихгүй.
- [ ] 10k+ жижиг файл, Unicode урт нэр, 2+ GB ZIP/TAR, цуцлалт, диск дүүрэх нөхцөл.
- [ ] BILZ `.001` + бусад хэсэг, дутуу/гэмтсэн хэсгийг шалгана.
- [ ] BILZ 1 GiB/file, 4 GiB total prototype хязгаарыг хэрэглэгчид ойлгомжтой харуулна.

## 4. Windows интеграц ба UI
- [ ] Classic Right-click: файл, хавтас, олон сонголт; ZIP/BILZ үүсгэх.
- [ ] Installer upgrade болон uninstall; Registry-д icon-гүй цэс үлдэхгүй.
- [ ] Өөр админ хэрэглэгчээр uninstall хийвэл per-user Registry үлдэж болохыг шалгах.
- [ ] ISO mount/unmount (зөвхөн хэрэглэгчийн хүсэлтээр).
- [ ] Монгол/Англи: Settings, Diagnostics, Extraction, Progress, About, More icon.
- [ ] Tray/single-instance, minimize, app exit, Theme system/dark/light.

## 5. Нээлттэй эх код
- [ ] SharpCompress, SharpZipLib болон transitive dependencies лицензийг шалгасан.
- [ ] Apache 2.0 NOTICE болон зураг/icon ашиглах эрхийг шалгасан.
- [ ] README, changelog, security policy, privacy notice, donation URL бодит болсон.
- [ ] Installer/portable SHA-256, Windows SmartScreen/signing шаардлагыг үнэлсэн.

**Хүлээн авах нөхцөл:** Дээрх бүх шаардлага биелтэл v1.0 RC1-ийг stable гэж нэрлэхгүй.
