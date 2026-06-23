namespace kingsightapi.Services;

/// <summary>
/// Maps <see cref="Entities.UserDto.UserId"/> (int) to <c>mort.CMHC_upload_historytbl.uploaded_by</c> (UNIQUEIDENTIFIER)
/// without altering the warehouse column type. Pattern: 00000000-0000-0000-0000-{userId as 12 hex digits}.
/// All-zero GUID remains the legacy "system" placeholder.
/// </summary>
internal static class UploadUserIdentityCodec
{
    private const string GuidPrefix = "00000000-0000-0000-0000-";

    public static Guid ToStorageGuid(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "userId must be positive.");
        }

        return Guid.Parse($"{GuidPrefix}{userId:x12}");
    }

    public static int? TryParseUserId(Guid storageGuid)
    {
        if (storageGuid == Guid.Empty)
        {
            return null;
        }

        var formatted = storageGuid.ToString("D");
        if (!formatted.StartsWith(GuidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tail = formatted.AsSpan(GuidPrefix.Length);
        return int.TryParse(tail, System.Globalization.NumberStyles.HexNumber, null, out var userId) && userId > 0
            ? userId
            : null;
    }
}
