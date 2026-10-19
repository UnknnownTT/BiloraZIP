using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace BiloraZIP.Services;

/// <summary>Two-language, runtime-refreshable UI text. Values are never machine translated.</summary>
public static class LocalizationService
{
    private static readonly ConditionalWeakTable<object, Dictionary<string, string>> Sources = new();
    private static readonly List<WeakReference<Window>> Windows = new();
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BiloraZIP", "language.txt");

    public static string Language { get; private set; } = "en";
    public static bool IsMongolian => Language == "mn";

    private static readonly Dictionary<string, string> Mongolian = new(StringComparer.Ordinal)
    {
        ["ZIP encryption protects file contents; file and folder names remain visible."] = "ZIP шифрлэл нь файлын агуулгыг хамгаална. Файл, хавтасны нэр харагдсаар байна.",
        ["BiloraZIP Settings"] = "BiloraZIP тохиргоо", ["Settings…"] = "Нарийвчилсан тохиргоо…",
        ["Save settings"] = "Тохиргоо хадгалах", ["General"] = "Ерөнхий",
        ["Test archives after creation by default"] = "Архив үүсгэсний дараа шалгах",
        ["Show donation invitation on the welcome screen"] = "Эхлэх дэлгэцэд дэмжлэгийн урилга харуулах",
        ["Show detailed error messages"] = "Алдааны дэлгэрэнгүй мэдээлэл харуулах",
        ["Extraction security"] = "Задлах үеийн хамгаалалт",
        ["Warn when ZIP contains both encrypted and unencrypted files"] = "Шифрлэсэн болон хамгаалалтгүй файл холилдсон ZIP-д анхааруулах",
        ["Safety limits for untrusted archives (adjust only when needed)."] = "Итгэлгүй архивын аюулгүй байдлын хязгаар (шаардлагатай үед л өөрчил).",
        ["Maximum extracted size (GiB)"] = "Нийт задлах дээд хэмжээ (GiB)",
        ["Maximum size per file (GiB)"] = "Нэг файлын дээд хэмжээ (GiB)",
        ["Maximum archive entries"] = "Архивын бичлэгийн дээд тоо",
        ["Maximum compression ratio (N:1)"] = "Шахалтын харьцааны хязгаар (N:1)",
        ["Support BiloraZIP"] = "BiloraZIP-ийг дэмжих", ["Support BiloraZIP ❤️"] = "BiloraZIP-ийг дэмжих ❤️",
        ["Optional HTTPS donation link (set by project owner; no payments processed inside the app)."] = "Donation HTTPS холбоос (төслийн эзэмшигч тохируулна; программ дотор төлбөр авахгүй).",
        ["Preferences are stored locally. Archive passwords are never saved."] = "Тохиргоо зөвхөн энэ компьютерт хадгалагдана. Архивын нууц үгийг хадгалахгүй.",
        ["The project owner has not configured a donation link yet."] = "Төслийн эзэмшигч donation холбоос тохируулаагүй байна.",
        ["BiloraZIP is free and open source. If you wish, you can support its continued development with a voluntary donation."] = "BiloraZIP бол үнэгүй, нээлттэй эхийн программ. Хэрэв хүсвэл сайн дурын дэмжлэгээр хөгжүүлэлтийг нь дэмжээрэй.",
        ["Donations are optional. No features are locked, and BiloraZIP does not collect payment details."] = "Дэмжлэг өгөх нь сайн дурынх. Ямар ч боломж түгжигдэхгүй, BiloraZIP төлбөрийн мэдээлэл цуглуулахгүй.",
        ["Open donation link"] = "Дэмжлэгийн холбоос нээх",
        ["If you wish, support BiloraZIP with a donation ❤️"] = "Хэрэв та хүсвэл Donation хийж Bilora-г дэмжээрэй ❤️",
        ["Made in Mongolia 🇲🇳"] = "Монголд бүтээв 🇲🇳",
        ["Open-source Windows archive manager • Apache License 2.0. ChatGPT assisted the development; archival operations run locally. Not affiliated with WinRAR."] = "Apache 2.0 лицензтэй Windows архивын программ. Хөгжүүлэлтэд ChatGPT тусалсан, архивын үйлдлүүд локал ажиллана. WinRAR-тай холбоогүй.",
        ["Explorer & file associations"] = "Explorer болон файлын холболт",
        ["Choose archive formats BiloraZIP can offer to open. This will not change your Windows default app without your confirmation in Settings."] = "BiloraZIP-ээр нээх архивын форматыг сонгоно уу. Windows-ийн үндсэн программыг таны зөвшөөрөлгүй өөрчлөхгүй.",
        ["Show BiloraZIP in the classic right-click menu"] = "Хуучин Right-click цэсэнд BiloraZIP-ийг харуулах",
        ["Windows 11 normally displays legacy right-click entries under 'Show more options'. All entries are registered only for the current user."] = "Windows 11-д хуучин командууд 'Show more options' хэсэгт харагдана. Энэ тохиргоо одоогийн хэрэглэгчид үйлчилнэ.",
        ["Machine-wide installer settings may also add entries. To change those, rerun the installer. The Windows default archive app must be chosen by you."] = "Installer бүх хэрэглэгчид зориулсан команд нэмсэн байж болно. Өөрчлөхийн тулд installer-ийг дахин ажиллуулна. Үндсэн программыг Windows тохиргооноос сонгоно.",
        ["Windows Default Apps"] = "Windows үндсэн программ", ["Apply for this user"] = "Энэ хэрэглэгчид хэрэгжүүлэх",
        ["Per-user Open With and classic Explorer commands were updated. Your Windows default app was not changed."] = "Энэ хэрэглэгчийн Open With болон Explorer команд шинэчлэгдлээ. Windows үндсэн программ өөрчлөгдөөгүй.",
        ["Integration failed"] = "Холболт амжилтгүй",
        ["Extracting archive"] = "Архив задлаж байна", ["Extracting"] = "Задалж байна", ["Testing archive readability"] = "Архив шалгаж байна",
        ["Preparing files for Explorer"] = "Explorer-д зориулж файл бэлдэж байна",
        ["Completed"] = "Амжилттай дууслаа", ["Cancelled"] = "Цуцлагдлаа", ["Failed"] = "Амжилтгүй",
        ["Cancelling…"] = "Цуцалж байна…", ["Calculating…"] = "Тооцоолж байна…",
        ["Show progress"] = "Явцыг харуулах",
        ["Encrypted archive — BiloraZIP"] = "Нууц үгтэй архив — BiloraZIP",
        ["Enter the archive password"] = "Архивын нууц үгийг оруулна уу",
        ["Total messages:"] = "Нийт мэдээлэл:",
        ["Windows Integration — BiloraZIP"] = "Windows холболт — BiloraZIP",

        ["BiloraZIP — Working"] = "BiloraZIP — Ажиллаж байна",
        ["BiloraZIP — Diagnostic messages"] = "BiloraZIP — Алдааны мэдээлэл",




        ["BiloraZIP — Archive Manager"] = "BiloraZIP — Архивын программ",
        ["Version"] = "Хувилбар", ["Version unavailable"] = "Хувилбар тодорхойгүй",
        ["0 files"] = "0 файл",
        ["Choose a drive or common location"] = "Диск эсвэл хавтас сонгоно уу",
        ["Open archive"] = "Архив нээх",
        ["Extracting BILZ"] = "BILZ задлаж байна",
        ["BILZ archive created"] = "BILZ архив үүслээ",
        ["BILZ integrity verified"] = "BILZ архив шалгагдлаа",
        ["Compressing (AES-256)"] = "AES-256-аар шахаж байна",
        ["Compressing"] = "Шахаж байна", ["Testing"] = "Шалгаж байна",
        ["_File"] = "_Файл", ["_Commands"] = "_Команд", ["_Tools"] = "_Хэрэгсэл",
        ["F_avorites"] = "_Дуртай", ["_Options"] = "_Тохиргоо", ["_Help"] = "_Тусламж",
        ["_Language"] = "_Хэл", ["English"] = "English", ["Монгол"] = "Монгол",
        ["Open archive…"] = "Архив нээх…", ["Open archive..."] = "Архив нээх…",
        ["Open archive folder"] = "Архивын хавтас нээх", ["Create archive from files…"] = "Файлаар архив үүсгэх…",
        ["Create archive from folder…"] = "Хавтсаар архив үүсгэх…",
        ["Close archive"] = "Архив хаах", ["Select all"] = "Бүгдийг сонгох",
        ["Copy selected file names"] = "Сонгосон файлын нэр хуулах",
        ["Find in archive…"] = "Архиваас хайх…", ["Find in archive"] = "Архиваас хайх",
        ["Archive information…"] = "Архивын мэдээлэл…", ["Archive information"] = "Архивын мэдээлэл",
        ["Exit"] = "Гарах", ["Extract to specified folder…"] = "Сонгосон хавтаст задлах…",
        ["Extract Here"] = "Энд задлах", ["Extract to named folder"] = "Нэрлэсэн хавтаст задлах",
        ["Test archived files"] = "Архивын файлуудыг шалгах", ["View selected text file"] = "Сонгосон файлыг нээх",
        ["Delete selected entry from archive"] = "Архиваас сонгосныг устгах",
        ["Quick archive wizard…"] = "Архивын туслах…",
        ["Open archive location"] = "Архивын байршил", ["Copy selected names"] = "Сонгосон нэрсийг хуулах",
        ["Scan archive with Microsoft Defender…"] = "Microsoft Defender-ээр шалгах…",
        ["Recover readable ZIP entries…"] = "ZIP-ийн уншигдах файлууд сэргээх…",
        ["Diagnostic messages…"] = "Алдааны мэдээлэл…",
        ["Mount ISO image (read-only)…"] = "ISO-г виртуал дискээр холбох…",
        ["Unmount ISO image…"] = "ISO виртуал дискийг салгах…",
        ["Explorer mouse-wheel scroll lines (1–15)"] = "Explorer-ийн хулганы дугуйгаар гүйлгэх мөр (1–15)",
        ["Extraction options"] = "Задлах тохиргоо",
        ["TAR.GZ (gzip-compressed TAR)"] = "TAR.GZ (gzip шахалттай TAR)",
        ["Split BILZ into volumes"] = "BILZ архивыг хэсэглэх",
        ["One file (.bilz)"] = "Нэг файл (.bilz)",
        ["100 MiB per part (.bilz.001…)"] = "100 MiB хэсэг (.bilz.001…)",
        ["700 MiB per part (.bilz.001…)"] = "700 MiB хэсэг (.bilz.001…)",
        ["1024 MiB per part (.bilz.001…)"] = "1024 MiB хэсэг (.bilz.001…)",
        ["4096 MiB per part (.bilz.001…)"] = "4096 MiB хэсэг (.bilz.001…)",
        ["All parts must be in one folder. Open part .001 to read the archive."] = "Бүх хэсгийг нэг хавтсанд байрлуулна. .001 хэсгийг нээж уншина.",
        ["Encryption is available for ZIP only. TAR, TAR.GZ and BILZ do not support passwords."] = "Нууц үг зөвхөн ZIP-д. TAR, TAR.GZ, BILZ нууц үг дэмжихгүй.",
        ["Compressing TAR.GZ"] = "TAR.GZ шахаж байна",
        ["TAR.GZ created"] = "TAR.GZ үүслээ",

        ["Extraction options — BiloraZIP"] = "Задлах тохиргоо — BiloraZIP",
        ["Destination folder"] = "Задлах хавтас",
        ["When a file already exists"] = "Ижил нэртэй файл байвал",
        ["Stop without overwriting (safest)"] = "Дарж бичихгүй, зогсоох (аюулгүй)",
        ["Skip existing files"] = "Байгаа файлуудыг алгасах",
        ["Rename new files automatically (name (1).ext)"] = "Шинэ файлын нэрийг өөрчлөх (name (1).ext)",
        ["Replace existing files (confirmation required)"] = "Байгаа файлыг солих (баталгаажуулна)",
        ["Open destination in Explorer after extraction"] = "Задласны дараа Explorer-д нээх",
        ["Existing folders are reused. Unsafe links and archive path traversal remain blocked."] = "Байгаа хавтсыг ашиглана. Аюултай холбоос, хавтаснаас гарах оролдлогыг хориглоно.",
        ["Selected message — select text with your mouse (Ctrl+C to copy)"] = "Сонгосон алдаа — хулганаар сонгож Ctrl+C дарж хуулна",

        ["Add current archive to favorites"] = "Одоогийн архивыг дуртайд нэмэх",
        ["Dark theme"] = "Харанхуй загвар", ["Light theme"] = "Гэрэлтэй загвар",
        ["Windows integration & associations…"] = "Windows холболт ба файлын төрөл…",
        ["Windows default app settings…"] = "Windows анхдагч программ…",
        ["About Bilora"] = "Bilora-ийн тухай", ["Add"] = "Нэмэх", ["Extract"] = "Задлах",
        ["Test"] = "Шалгах", ["Open"] = "Нээх", ["Delete"] = "Устгах",
        ["Find"] = "Хайх", ["Info"] = "Мэдээлэл", ["⋯  More"] = "⋯  Бусад",
        ["Create ZIP from folder…"] = "Хавтсаар ZIP үүсгэх…",
        ["Add to Favorites"] = "Дуртайд нэмэх", ["Windows integration…"] = "Windows холболт…",
        ["Scan archive…"] = "Архив шалгах…", ["Recover ZIP…"] = "ZIP сэргээх…",
        ["This PC"] = "Энэ компьютер", ["Ready"] = "Бэлэн",
        ["Welcome to BiloraZIP"] = "BiloraZIP-д тавтай морил", ["Browse folders or open an archive to see its contents."] = "Хавтаснаас сонгох эсвэл архив нээгээд файлуудыг үзнэ үү.",
        ["Property"] = "Үзүүлэлт", ["Value"] = "Утга",
        ["Options"] = "Тохиргоо", ["Comment"] = "Тайлбар", ["SFX"] = "SFX",
        ["This is a read-only report. Compression settings depend on the archive format and engine."] = "Энэ нь зөвхөн унших тайлан. Шахалтын тохиргоо нь формат, хөдөлгүүрээс хамаарна.",
        ["Not available"] = "Боломжгүй", ["SFX metadata is not exposed by the current archive engine."] = "Одоогийн архивын хөдөлгүүр SFX мэдээлэл гаргахгүй.",
        ["Diagnostic messages"] = "Алдааны мэдээлэл", ["Level"] = "Түвшин",
        ["Message"] = "Мэдээлэл", ["Close"] = "Хаах", ["Break operation"] = "Үйлдлийг зогсоох",
        ["Copy to clipboard"] = "Хуулах", ["Create archive"] = "Архив үүсгэх",
        ["Archive name and location"] = "Архивын нэр, байршил", ["Browse…"] = "Сонгох…",
        ["Archive format"] = "Архивын формат", ["RAR/7Z creation unavailable"] = "RAR/7Z үүсгэх боломжгүй",
        ["RAR/7Z creation unavailable; BILZ has no encryption"] = "RAR/7Z үүсгэх боломжгүй. BILZ нууц үг дэмжихгүй",
        ["BILZ (experimental)"] = "BILZ (туршилтын)",
        ["Compression level"] = "Шахалтын түвшин", ["Store (no compression)"] = "Шахахгүй хадгалах",
        ["Fastest"] = "Хурдан", ["Normal / Optimal"] = "Ердийн / тохиромжтой",
        ["Smallest size"] = "Хамгийн жижиг", ["Test archive after creating"] = "Үүсгэсний дараа шалгах",
        ["Encrypt ZIP with AES-256 password"] = "ZIP файлыг AES-256 нууц үгээр хамгаалах",
        ["Password"] = "Нууц үг", ["Confirm password"] = "Нууц үгийг давтах",
        ["Encryption is available for ZIP only. TAR does not support passwords."] = "Нууц үгийн хамгаалалт зөвхөн ZIP-д байна. TAR дэмжихгүй.",
        ["Encryption is available for ZIP only. TAR and BILZ do not support passwords."] = "Нууц үгийн хамгаалалт зөвхөн ZIP-д байна. TAR, BILZ дэмжихгүй.",
        ["Create"] = "Үүсгэх", ["Cancel"] = "Болих", ["Unlock"] = "Нээх",
        ["Archive password required"] = "Архивын нууц үг шаардлагатай",
        ["BiloraZIP does not save this password to disk."] = "BiloraZIP энэ нууц үгийг диск дээр хадгалахгүй.",
        ["Elapsed time"] = "Өнгөрсөн хугацаа", ["Time remaining (estimate)"] = "Үлдсэн хугацаа (тооцоо)",
        ["Background"] = "Ард ажиллуулах", ["Processing"] = "Боловсруулж байна", ["Preparing…"] = "Бэлдэж байна…",
        ["Open-source archive manager for Windows"] = "Windows-д зориулсан нээлттэй эхийн архивын программ",
        ["Created by Bilguun Munkhzul"] = "Бүтээсэн: Bilguun Munkhzul",
        ["Powered By ChatGPT"] = "ChatGPT-ийн тусламжтай хөгжүүлэв",
        ["Minimize to tray"] = "Системийн буланд нуух", ["Exit BiloraZIP"] = "BiloraZIP-ээс гарах",
        ["Keep working"] = "Үргэлжлүүлэх", ["Remember this choice"] = "Энэ сонголтыг санах",
        ["Extract selected…"] = "Сонгосныг задлах…", ["Extract selected"] = "Сонгосныг задлах",
        ["Only the selected files and folders will be extracted."] = "Зөвхөн сонгосон файл, хавтас задлагдана.",
        ["Yes"] = "Тийм",
        ["No"] = "Үгүй",
        ["OK"] = "Зөвшөөрөх",
        ["Attention"] = "Анхааруулга",
        ["Error"] = "Алдаа",
        ["Cut"] = "Зөөхөөр хайчлах",
        ["Copy"] = "Хуулах",
        ["Paste"] = "Буулгах",
        ["Rename"] = "Нэр солих",
        ["Properties"] = "Шинж чанар",
        ["New folder"] = "Шинэ хавтас",
        ["Folder name"] = "Хавтасны нэр",
        ["New name"] = "Шинэ нэр",
        ["Add to archive…"] = "Архивт нэмэх…",
        ["Create ZIP archive"] = "ZIP архив үүсгэх",
        ["Open entry"] = "Файл нээх",
        ["File"] = "Файл",
        ["Folder"] = "Хавтас",
        ["Size"] = "Хэмжээ",
        ["Name"] = "Нэр",
        ["Location"] = "Байршил",
        ["Modified"] = "Өөрчилсөн",
        ["Type"] = "Төрөл",
        ["Archive type"] = "Архивын төрөл",
        ["Full path"] = "Бүтэн зам",
        ["Archive size"] = "Архивын хэмжээ",
        ["Total files"] = "Нийт файл",
        ["Total folders"] = "Нийт хавтас",
        ["Original size"] = "Анхны хэмжээ",
        ["Packed contents size"] = "Шахсан хэмжээ",
        ["Compression ratio"] = "Шахалтын харьцаа",
        ["Encryption"] = "Шифрлэл",
        ["Headers encrypted"] = "Толгой хэсгийн шифрлэл",
        ["Dictionary size"] = "Толь бичгийн хэмжээ",
        ["Recovery record"] = "Сэргээх бичлэг",
        ["Integrity"] = "Бүрэн бүтэн байдал",
        ["Exit completely or keep BiloraZIP in the Windows system tray?"] = "BiloraZIP-ийг бүрэн хаах уу, эсвэл системийн буланд үлдээх үү?",
        ["You can restore BiloraZIP by double-clicking its tray icon."] = "Системийн булан дахь BiloraZIP дүрсэн дээр хоёр товшиж дахин нээнэ.",
        ["Open an archive first."] = "Эхлээд архив нээнэ үү.",
        ["Open an archive to view its information."] = "Архивын мэдээллийг харахын тулд эхлээд архив нээнэ үү.",
        ["Invalid destination path."] = "Хадгалах зам буруу байна.",
        ["Archive filename extension must match its selected format."] = "Архивын өргөтгөл сонгосон форматтай таарах ёстой.",
        ["A file already exists at this destination. Choose a new name."] = "Энэ байршилд ижил нэртэй файл байна. Өөр нэр сонгоно уу.",
        ["The destination folder does not exist."] = "Хадгалах хавтас олдсонгүй.",
        ["Enter a password and confirm it exactly. Empty passwords are not supported."] = "Нууц үгийг оруулаад ижил байдлаар давтана уу. Хоосон байж болохгүй.",
        ["Enter the password first."] = "Эхлээд нууц үгээ оруулна уу.",
        ["Invalid file or folder name."] = "Файл эсвэл хавтасны нэр буруу байна.",
        ["A file or folder with this name already exists."] = "Ийм нэртэй файл эсвэл хавтас байна.",
        ["Move selected items to Recycle Bin?"] = "Сонгосон зүйлсийг Хогийн сав руу шилжүүлэх үү?",
        ["Permanently delete the selected items?"] = "Сонгосон зүйлсийг бүрмөсөн устгах уу?",
        ["Remove selected entries from ZIP permanently? This cannot be undone."] = "Сонгосон ZIP файлуудыг бүрмөсөн устгах уу? Буцаах боломжгүй.",
        ["Items ready to move."] = "Зөөхөөр бэлдлээ.",
        ["Items copied to clipboard."] = "Файлуудыг хууллаа.",
        ["Archive name and parameters — BiloraZIP"] = "Архивын нэр ба тохиргоо — BiloraZIP",
        ["BiloraZIP — Exit Options"] = "BiloraZIP — Гарах сонголтууд",
        ["BiloraZIP — Archive information"] = "BiloraZIP — Архивын мэдээлэл",
        ["This archive is no longer at the saved location."] = "Архив хадгалсан байршлаасаа алга болсон байна.",
        ["This path is not an existing folder or supported archive."] = "Энэ замд хавтас эсвэл дэмждэг архив олдсонгүй.",
        ["System tray is not available on this desktop."] = "Энэ орчинд системийн булан ашиглах боломжгүй байна.",
    };

    public static string Translate(string english) => IsMongolian && Mongolian.TryGetValue(english, out var mn) ? mn : english;

    public static void Initialize()
    {
        try { if (File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Trim() == "mn") Language = "mn"; }
        catch (IOException) { }
    }

    public static void Register(Window window)
    {
        Windows.Add(new WeakReference<Window>(window));
        window.Loaded += (_, _) => { Apply(window); WindowThemeService.Apply(window); };
    }

    public static void SetLanguage(string code)
    {
        Language = code == "mn" ? "mn" : "en";
        try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath, Language); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Windows.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var weak in Windows)
            if (weak.TryGetTarget(out var window)) Apply(window);
    }

    public static void Apply(Window window)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ApplyObject(window, visited);
    }

    private static void ApplyObject(object obj, HashSet<object> visited)
    {
        if (!visited.Add(obj)) return;
        switch (obj)
        {
            case Window w: ApplyValue(w, "Title", () => w.Title, s => w.Title = s); break;
            case TextBlock b: ApplyValue(b, "Text", () => b.Text, s => b.Text = s); break;
            case HeaderedItemsControl h when h.Header is string:
                ApplyValue(h, "Header", () => h.Header as string ?? "", s => h.Header = s); break;
            case HeaderedContentControl h when h.Header is string:
                ApplyValue(h, "Header", () => h.Header as string ?? "", s => h.Header = s); break;
            case ContentControl c when c.Content is string:
                ApplyValue(c, "Content", () => c.Content as string ?? "", s => c.Content = s); break;
        }
        if (obj is System.Windows.Controls.DataGrid grid)
            foreach (var column in grid.Columns)
                if (column.Header is string) ApplyValue(column, "Header", () => column.Header as string ?? "", s => column.Header = s);
        if (obj is ItemsControl items)
            foreach (var child in items.Items)
                if (child is not null && child is not string) ApplyObject(child, visited);
        if (obj is DependencyObject dependency)
            foreach (var child in LogicalTreeHelper.GetChildren(dependency))
                if (child is not null && child is not string) ApplyObject(child, visited);
    }

    private static void ApplyValue(object obj, string kind, Func<string> read, Action<string> write)
    {
        var map = Sources.GetOrCreateValue(obj);
        if (!map.TryGetValue(kind, out string? source))
        {
            string current = read();
            if (!Mongolian.ContainsKey(current)) return;
            map[kind] = source = current;
        }
        write(Translate(source));
    }
}
