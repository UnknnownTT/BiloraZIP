using System.IO;

namespace BiloraZIP.Services;

public static class ErrorMessageService
{
    public static string Describe(Exception error)
    {
        if (!LocalizationService.IsMongolian) return error.Message;
        string message = error.Message;
        if (ArchiveReadService.IsPasswordException(error))
            return "Нууц үг буруу, дутуу эсвэл архивын шифрлэл дэмжигдээгүй байна. " + message;
        if (message.Contains("Destination exists", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return "Задлах хавтсанд ижил нэртэй файл эсвэл хавтас байна. " + message;
        if (message.Contains("safety limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("compression ratio", StringComparison.OrdinalIgnoreCase))
            return "Архив аюулгүй байдлын хязгаарыг давлаа. Тохиргоо → Задлах үеийн хамгаалалтыг шалгана уу. " + message;
        if (message.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("symbolic link", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("junction", StringComparison.OrdinalIgnoreCase))
            return "Архивт аюултай зам эсвэл холбоос илэрсэн тул задлахыг хориглолоо. " + message;
        if (error is InvalidDataException)
            return "Архив эвдэрсэн, дэмжигдээгүй эсвэл доторх мэдээлэл нь зөрчилтэй байна. " + message;
        if (error is UnauthorizedAccessException)
            return "Файл эсвэл хавтсанд хандах эрх хүрэлцэхгүй байна. " + message;
        if (error is IOException)
            return "Файл унших/бичих явцад алдаа гарлаа. " + message;
        return "Үйлдэл амжилтгүй: " + message;
    }
}
