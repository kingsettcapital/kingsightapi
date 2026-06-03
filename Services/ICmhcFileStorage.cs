namespace kingsightapi.Services
{
    /// <summary>
    /// CMHC upload file storage abstraction.
    /// Phase 1: <see cref="LocalCmhcFileStorage"/> writes to <c>CmhcUpload:LocalStoragePath</c>.
    /// Phase 2: <c>FabricCmhcFileStorage</c> can upload to OneLake; DB keeps logical filename only.
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
