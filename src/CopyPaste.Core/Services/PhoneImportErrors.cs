using CopyPaste.Core.Models;

namespace CopyPaste.Core.Services;

/// <summary>Raised when a device read or open makes no progress within the stall timeout.</summary>
public sealed class PhoneImportStallException(string message) : TimeoutException(message);

/// <summary>Raised when the temporary copy does not hash to the bytes read from the device.</summary>
public sealed class PhoneImportVerificationException(string message) : IOException(message);

/// <summary>Maps device and file-system errors to import decisions and bilingual guidance.</summary>
public static class PhoneImportErrors
{
    // ERROR_GEN_FAILURE: "A device attached to the system is not functioning." iPhones return it when the
    // "Automatic" transfer format fails to convert an item (HEIC → JPG, HEVC → H.264).
    public const int DeviceNotFunctioningHResult = unchecked((int)0x8007001F);

    private static readonly HashSet<int> DisconnectedHResults =
    [
        unchecked((int)0x8007048F), // ERROR_DEVICE_NOT_CONNECTED
        unchecked((int)0x80070015), // ERROR_NOT_READY
        unchecked((int)0x80070079), // ERROR_SEM_TIMEOUT
        unchecked((int)0x800701B1), // ERROR_NO_SUCH_DEVICE
        unchecked((int)0x80070490), // ERROR_NOT_FOUND
        unchecked((int)0x80070037), // ERROR_DEV_NOT_EXIST
        unchecked((int)0x8007045D)  // ERROR_IO_DEVICE
    ];

    private static readonly HashSet<int> DestinationFullHResults =
    [
        unchecked((int)0x80070070), // ERROR_DISK_FULL
        unchecked((int)0x80070027)  // ERROR_HANDLE_DISK_FULL
    ];

    public static PhoneImportErrorKind Classify(Exception exception)
    {
        switch (exception)
        {
            case OperationCanceledException:
                return PhoneImportErrorKind.Cancelled;
            case TimeoutException:
                return PhoneImportErrorKind.DeviceDisconnected;
            case PhoneImportVerificationException:
                return PhoneImportErrorKind.VerificationFailed;
        }
        var kind = ClassifyHResult(exception.HResult);
        if (kind != PhoneImportErrorKind.Other)
            return kind;
        return exception switch
        {
            UnauthorizedAccessException => PhoneImportErrorKind.AccessDenied,
            PathTooLongException => PhoneImportErrorKind.PathTooLong,
            _ when exception.InnerException is { } inner => Classify(inner),
            _ => PhoneImportErrorKind.Other
        };
    }

    public static PhoneImportErrorKind ClassifyHResult(int hresult)
    {
        if (hresult == DeviceNotFunctioningHResult)
            return PhoneImportErrorKind.DeviceNotFunctioning;
        if (DisconnectedHResults.Contains(hresult))
            return PhoneImportErrorKind.DeviceDisconnected;
        if (DestinationFullHResults.Contains(hresult))
            return PhoneImportErrorKind.DestinationFull;
        return hresult switch
        {
            unchecked((int)0x80070005) => PhoneImportErrorKind.AccessDenied,
            // ERROR_FILENAME_EXCED_RANGE, ERROR_BUFFER_OVERFLOW ("The file name is too long").
            unchecked((int)0x800700CE) or unchecked((int)0x8007006F) => PhoneImportErrorKind.PathTooLong,
            unchecked((int)0x8007007B) => PhoneImportErrorKind.InvalidName,
            _ => PhoneImportErrorKind.Other
        };
    }

    /// <summary>Errors after which every remaining item would fail too.</summary>
    public static bool StopsImport(PhoneImportErrorKind kind) =>
        kind is PhoneImportErrorKind.DeviceDisconnected or PhoneImportErrorKind.DestinationFull;

    public static string FormatHResult(int hresult) => $"0x{hresult:X8}";

    public static string Describe(PhoneImportErrorKind kind, bool english) => kind switch
    {
        PhoneImportErrorKind.DeviceNotFunctioning => english
            ? "The device could not deliver the file. On an iPhone this is usually a conversion failure of the \"Automatic\" transfer format."
            : "Aygıt dosyayı veremedi. iPhone'da bu genellikle \"Otomatik\" aktarım biçimindeki dönüştürme hatasıdır.",
        PhoneImportErrorKind.DeviceDisconnected => english
            ? "The connection to the device was lost or the device stopped responding."
            : "Aygıtla bağlantı koptu ya da aygıt yanıt vermiyor.",
        PhoneImportErrorKind.AccessDenied => english ? "Access was denied." : "Erişim reddedildi.",
        PhoneImportErrorKind.DestinationFull => english
            ? "The destination drive is out of space."
            : "Hedef sürücüde yer kalmadı.",
        PhoneImportErrorKind.PathTooLong => english
            ? "The destination path is too long for Windows."
            : "Hedef yol Windows için çok uzun.",
        PhoneImportErrorKind.InvalidName => english
            ? "The file name is not valid in Windows."
            : "Dosya adı Windows'ta geçersiz.",
        PhoneImportErrorKind.DestinationConflict => english
            ? "A folder with the same name already exists at the destination."
            : "Hedefte aynı adla bir klasör var.",
        PhoneImportErrorKind.VerificationFailed => english
            ? "Verification failed: the written file did not match the data read from the device, so it was removed."
            : "Doğrulama başarısız: yazılan dosya aygıttan okunan veriyle eşleşmedi, bu yüzden silindi.",
        PhoneImportErrorKind.Cancelled => english ? "Cancelled." : "İptal edildi.",
        _ => english ? "Read or write error." : "Okuma ya da yazma hatası."
    };

    public static string KeepOriginalsHint(bool english) => english
        ? "On the iPhone, open Settings > Photos > \"Transfer to Mac or PC\", choose \"Keep Originals\", and try again."
        : "iPhone'da Ayarlar > Fotoğraflar > \"Mac'e veya PC'ye Aktar\" bölümünde \"Orijinalleri Koru\"yu seçip yeniden deneyin.";

    public static string LockedOrUntrustedHint(bool english) => english
        ? "Unlock the phone and tap \"Trust\" when it asks \"Trust This Computer?\", then select Refresh. On Android, choose \"File transfer\" in the USB notification."
        : "Telefonun kilidini açın ve ekrandaki \"Bu bilgisayara güvenilsin mi?\" sorusuna \"Güven\" deyin; sonra Yenile'ye basın. Android'de USB bildiriminden \"Dosya aktarımı\"nı seçin.";

    public static IReadOnlyList<string> NoDeviceGuidance(bool english) => english
        ?
        [
            "Connect the phone with a USB cable that carries data, not a charge-only cable.",
            "Unlock the phone; if the iPhone asks \"Trust This Computer?\", tap \"Trust\".",
            "On Android, choose \"File transfer\" in the USB notification.",
            "An iPhone should appear as \"Apple iPhone\" under \"This PC\" in File Explorer; if it doesn't, reconnect the cable while the phone is unlocked.",
            "Then select Refresh."
        ]
        :
        [
            "Telefonu yalnız şarj eden değil, veri aktarabilen bir USB kablosuyla bağlayın.",
            "Telefonun kilidini açın; iPhone \"Bu bilgisayara güvenilsin mi?\" diye sorarsa \"Güven\" deyin.",
            "Android'de USB bildiriminden \"Dosya aktarımı\"nı seçin.",
            "iPhone, Dosya Gezgini'nde \"Bu bilgisayar\" altında \"Apple iPhone\" olarak görünmelidir; görünmüyorsa telefonun kilidi açıkken kabloyu çıkarıp yeniden takın.",
            "Sonra Yenile'ye basın."
        ];
}
