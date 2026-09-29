using kingsightapi.Entities;

namespace kingsightapi.Services
{
    public interface ICmhcUploadService
    {
        Task<IReadOnlyList<CmhcUploadHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken);

        Task<CmhcUploadHistoryDto> UploadAsync(
            IFormFile file,
            string fileName,
            int uploadedByUserId,
            string fileType,
            DateOnly asOfDate,
            CancellationToken cancellationToken);

        Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Streams a QR slide PDF for LTV Validation preview. Resolves file name from portal URL or path.
        /// </summary>
        Task<(Stream Stream, string FileName)> GetQrSlidePreviewAsync(
            string link,
            CancellationToken cancellationToken = default);
    }
}
