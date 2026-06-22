namespace kingsightapi.Entities;

public sealed class CmhcUploadFormRequest
{
    public IFormFile? File { get; set; }

    public string? FileName { get; set; }

    public string? UploadedBy { get; set; }

    public string? FileType { get; set; }
}
