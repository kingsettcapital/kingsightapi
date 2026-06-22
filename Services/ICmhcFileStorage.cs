namespace kingsightapi.Services
{
    /// <summary>
    /// CMHC upload file storage abstraction.
    /// Production: <see cref="FabricCmhcFileStorage"/> writes to Fabric OneLake
    /// (<c>Files/Uploaded files/...</c> per <see cref="CmhcUploadOptions"/>).
    /// Dev fallback: <see cref="LocalCmhcFileStorage"/> when <c>StorageProvider</c> is <c>Local</c>.
    /// </summary>
    public interface ICmhcFileStorage
    {
        void EnsureStorageReady();

        /// <summary>
        /// Saves upload content using a unique filename under storage (appends _1, _2, … if needed).
        /// </summary>
        /// <param name="uploadCategory"><see cref="CmhcUploadFileTypes.CmhcExcel"/> or <see cref="CmhcUploadFileTypes.QrSlides"/>.</param>
        /// <returns>Final filename stored on disk (not full path).</returns>
        Task<string> SaveUploadAsync(
            Stream content,
            string sanitizedFileName,
            string uploadCategory,
            CancellationToken cancellationToken);

        Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken);

        Task DeleteUploadAsync(
            string storedFileName,
            string uploadCategory,
            CancellationToken cancellationToken);
    }
}
