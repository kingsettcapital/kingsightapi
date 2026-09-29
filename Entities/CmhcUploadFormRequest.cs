namespace kingsightapi.Entities;

public sealed class CmhcUploadFormRequest
{
    public IFormFile? File { get; set; }

    public string? FileName { get; set; }

    /// <summary>Legacy UNIQUEIDENTIFIER string — prefer <see cref="UploadedByUserId"/>.</summary>
    public string? UploadedBy { get; set; }

    /// <summary>input.UserMst.UserId for the uploading user.</summary>
    public int? UploadedByUserId { get; set; }

    public string? FileType { get; set; }

    /// <summary>Document as-of / period date (yyyy-MM-dd).</summary>
    public string? AsOfDate { get; set; }
}
