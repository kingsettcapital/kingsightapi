namespace kingsightapi.Services
{
    /// <summary>
    /// CMHC upload file storage abstraction.
    /// Production: <see cref="FabricCmhcFileStorage"/> writes to Fabric OneLake (<c>CmhcUpload:FabricFilesPath</c>).
    /// Dev fallback: <see cref="LocalCmhcFileStorage"/> when <c>StorageProvider</c> is <c>Local</c>.
    /// </summary>
    public interface ICmhcFileStorage
    {
        void EnsureStorageReady();

        /// <summary>
        /// Saves upload content using a unique filename under storage (appends _1, _2, … if needed).
        /// </summary>
        /// <returns>Final filename stored on disk (not full path).</returns>
        Task<string> SaveUploadAsync(Stream content, string sanitizedFileName, CancellationToken cancellationToken);

        Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken);

        Task DeleteUploadAsync(string storedFileName, CancellationToken cancellationToken);
    }
}
