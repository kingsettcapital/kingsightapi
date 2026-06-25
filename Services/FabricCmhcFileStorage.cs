using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using kingsightapi.Configuration;
using Microsoft.Extensions.Options;

namespace kingsightapi.Services;

/// <summary>
/// Stores uploads in Fabric OneLake under
/// <c>Files/external_files/cmhc_file</c> and <c>Files/external_files/qr_slides</c>.
/// </summary>
public sealed class FabricCmhcFileStorage : ICmhcFileStorage
{
    private readonly CmhcUploadOptions _options;
    private readonly DataLakeServiceClient _serviceClient;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<FabricCmhcFileStorage> _logger;

    public FabricCmhcFileStorage(
        IOptions<CmhcUploadOptions> options,
        DataLakeServiceClient serviceClient,
        IHostEnvironment hostEnvironment,
        ILogger<FabricCmhcFileStorage> logger)
    {
        _options = options.Value;
        _serviceClient = serviceClient;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public void EnsureStorageReady()
    {
        EnsureDirectoryReady(GetUploadDirectoryClient(CmhcUploadFileTypes.CmhcExcel));
        EnsureDirectoryReady(GetUploadDirectoryClient(CmhcUploadFileTypes.QrSlides));
    }

    private void EnsureDirectoryReady(DataLakeDirectoryClient directory)
    {
        if (directory.Exists())
        {
            _logger.LogInformation(
                "File upload storage verified on Fabric OneLake at {Uri}",
                directory.Uri);
            return;
        }

        var created = directory.CreateIfNotExists();
        _logger.LogInformation(
            "File upload storage ready on Fabric OneLake at {Uri} (created={Created})",
            directory.Uri,
            created is not null);
    }

    public async Task<string> SaveUploadAsync(
        Stream content,
        string sanitizedFileName,
        string uploadCategory,
        CancellationToken cancellationToken)
    {
        var directory = GetUploadDirectoryClient(uploadCategory);
        await directory.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var existingNames = await ListUploadFileNamesAsync(directory, cancellationToken);
        var uniqueName = CmhcFileNameHelper.ResolveUniqueFileName(existingNames, sanitizedFileName);
        var fileClient = directory.GetFileClient(uniqueName);

        if (await fileClient.ExistsAsync(cancellationToken))
        {
            throw new IOException($"CMHC upload file already exists: {uniqueName}");
        }

        await fileClient.UploadAsync(content, overwrite: false, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Saved file upload {FileName} to Fabric OneLake path {Path}",
            uniqueName,
            _options.GetFabricUploadDirectoryPath(uploadCategory));

        return uniqueName;
    }

    public async Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken)
    {
        var directory = GetUploadDirectoryClient();
        var fabricTemplate = directory.GetFileClient(_options.TemplateFileName);
        if (await fabricTemplate.ExistsAsync(cancellationToken))
        {
            var response = await fabricTemplate.ReadAsync(cancellationToken: cancellationToken);
            return (response.Value.Content, _options.TemplateFileName);
        }

        var localPath = Path.Combine(
            _hostEnvironment.ContentRootPath,
            "wwwroot",
            "templates",
            _options.TemplateFileName);

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException(
                $"CMHC template file was not found in OneLake or at '{localPath}'.",
                _options.TemplateFileName);
        }

        Stream stream = new FileStream(
            localPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return (stream, _options.TemplateFileName);
    }

    public async Task DeleteUploadAsync(
        string storedFileName,
        string uploadCategory,
        CancellationToken cancellationToken)
    {
        var safeName = CmhcFileNameHelper.SanitizeFileName(storedFileName);
        var fileClient = GetUploadDirectoryClient(uploadCategory).GetFileClient(safeName);
        var deleted = await fileClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        if (deleted.Value)
        {
            _logger.LogInformation("Deleted file upload {FileName} from Fabric OneLake", safeName);
        }
    }

    private DataLakeDirectoryClient GetUploadDirectoryClient(string? uploadCategory = null)
    {
        var fileSystem = _serviceClient.GetFileSystemClient(_options.FabricFileSystemName);
        var directoryPath = uploadCategory is null
            ? _options.FabricUploadDirectoryPath
            : _options.GetFabricUploadDirectoryPath(CmhcUploadFileTypes.Normalize(uploadCategory));
        return fileSystem.GetDirectoryClient(directoryPath);
    }

    private static async Task<IReadOnlyList<string>> ListUploadFileNamesAsync(
        DataLakeDirectoryClient directory,
        CancellationToken cancellationToken)
    {
        var names = new List<string>();

        await foreach (var pathItem in directory.GetPathsAsync(
                           recursive: false,
                           cancellationToken: cancellationToken))
        {
            if (pathItem.IsDirectory != false)
            {
                continue;
            }

            var name = Path.GetFileName(pathItem.Name.TrimEnd('/'));
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }
}
