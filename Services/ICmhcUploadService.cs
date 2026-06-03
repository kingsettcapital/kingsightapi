using kingsightapi.Entities;

namespace kingsightapi.Services
{
    public interface ICmhcUploadService
    {
        Task<IReadOnlyList<CmhcUploadHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken);

        Task<CmhcUploadHistoryDto> UploadAsync(
            IFormFile file,
            string fileName,
            string uploadedBy,
            CancellationToken cancellationToken);

        Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken);
    }
}
