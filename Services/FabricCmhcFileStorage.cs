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

    public async Task<(Stream Stream, string FileName)> GetQrSlideAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var directory = GetUploadDirectoryClient(CmhcUploadFileTypes.QrSlides);
        var safeName = CmhcFileNameHelper.SanitizeFileName(fileName);
        var resolvedName = await ResolveQrSlideFileNameAsync(directory, safeName, cancellationToken);
        var fileClient = directory.GetFileClient(resolvedName);
        var response = await fileClient.ReadAsync(cancellationToken: cancellationToken);
        return (response.Value.Content, resolvedName);
    }

    public async Task<(Stream Stream, string FileName)> GetLakehouseFilesPathAsync(
        string filesRelativePath,
        string? lakehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = filesRelativePath.Trim().Replace('\\', '/').Trim('/');
        if (normalizedPath.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Lakehouse file path is invalid.", nameof(filesRelativePath));
        }

        var resolvedLakehouseId = NormalizeLakehouseId(lakehouseId ?? _options.FabricLakehouseId);
        var filePath = $"{resolvedLakehouseId}/Files/{normalizedPath}";
        var fileSystem = _serviceClient.GetFileSystemClient(_options.FabricFileSystemName);
        var fileClient = fileSystem.GetFileClient(filePath);
        if (!await fileClient.ExistsAsync(cancellationToken))
        {
            throw new FileNotFoundException(
                $"Lakehouse file '{normalizedPath}' was not found at {filePath}.",
                normalizedPath);
        }

        _logger.LogInformation("Opened lakehouse QR slide at {FilePath}", filePath);

        var response = await fileClient.ReadAsync(cancellationToken: cancellationToken);
        return (response.Value.Content, Path.GetFileName(normalizedPath));
    }

    private static string NormalizeLakehouseId(string lakehouseId)
    {
        var trimmed = lakehouseId.Trim();
        return trimmed.EndsWith(".Lakehouse", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^".Lakehouse".Length]
            : trimmed;
    }

    private static async Task<string> ResolveQrSlideFileNameAsync(
        DataLakeDirectoryClient directory,
        string safeName,
        CancellationToken cancellationToken)
    {
        var fileClient = directory.GetFileClient(safeName);
        if (await fileClient.ExistsAsync(cancellationToken))
        {
            return safeName;
        }

        var available = await ListUploadFileNamesAsync(directory, cancellationToken);
        var match = available.FirstOrDefault(
            name => name.Equals(safeName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        throw new FileNotFoundException(
            $"QR slide file '{safeName}' was not found in Fabric OneLake qr_slides storage.",
            safeName);
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
