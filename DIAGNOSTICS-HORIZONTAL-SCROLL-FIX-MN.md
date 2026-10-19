# BiloraZIP v0.9.4 — Алдааны цонхны хэвтээ scroll засвар

- DiagnosticsWindow.xaml: DataGrid-ийн horizontal/vertical scrollbar Auto.
- Message баганын Width="*"-ийг Width="SizeToCells" болгосон. Урт текст баганын хэмжээг томруулж, цонхны доод хэсэгт хэвтээ scroll гарна.
- Алдааны мэдээлэл мөрөөр нугарахгүй; mouse hover хийхэд бүтэн мэдээлэл ToolTip-ээр харагдана.
- Хуулах товч бүх алдааг clipboard-д хуулна.
- Архив задлах логикийг өөрчлөөгүй. "Destination exists" алдааг энэ UI засвар арилгахгүй.

Windows PowerShell: .\run-selftests.ps1, дараа нь .\build-installer.ps1
Windows дээр build болон GUI тест хийх шаардлагатай.
