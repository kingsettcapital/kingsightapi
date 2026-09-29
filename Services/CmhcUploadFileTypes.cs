using kingsightapi.Configuration;

namespace kingsightapi.Services;

public static class CmhcUploadFileTypes
{
    public const string CmhcExcel = CmhcUploadCategory.CmhcExcel;
    public const string QrSlides = CmhcUploadCategory.QrSlides;

    public static string Normalize(string? fileType)
    {
        if (IsQrSlides(fileType))
        {
            return QrSlides;
        }

        return CmhcExcel;
    }

    public static string Resolve(string? fileType, string? fileName)
    {
        if (IsQrSlides(fileType) || IsCmhc(fileType))
        {
            return Normalize(fileType);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return QrSlides;
        }

        if (IsCmhcExtension(extension))
        {
            return CmhcExcel;
        }

        return Normalize(fileType);
    }

    public static bool IsQrSlides(string? fileType) =>
        string.Equals(fileType?.Trim(), QrSlides, StringComparison.OrdinalIgnoreCase);

    public static bool IsCmhc(string? fileType) =>
        string.Equals(fileType?.Trim(), CmhcExcel, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupported(string? fileType, string? fileName = null)
    {
        if (IsQrSlides(fileType) || IsCmhc(fileType))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || IsCmhcExtension(extension);
    }

    private static bool IsCmhcExtension(string extension) =>
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase);
}
