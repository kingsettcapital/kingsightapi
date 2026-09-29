using kingsightapi.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace kingsightapi.Services
{
    public sealed class LocalCmhcFileStorage : ICmhcFileStorage
    {
        private readonly CmhcUploadOptions _options;
        private readonly string _contentRoot;
        private readonly ILogger<LocalCmhcFileStorage> _logger;
        private readonly Dictionary<string, string> _resolvedStoragePaths = new(StringComparer.OrdinalIgnoreCase);

        public LocalCmhcFileStorage(
            IOptions<CmhcUploadOptions> options,
            IHostEnvironment hostEnvironment,
            ILogger<LocalCmhcFileStorage> logger)
        {
            _options = options.Value;
            _contentRoot = hostEnvironment.ContentRootPath;
            _logger = logger;
        }

        public void EnsureStorageReady()
        {
            var storagePath = GetStoragePath(CmhcUploadFileTypes.CmhcExcel);
            Directory.CreateDirectory(storagePath);
            Directory.CreateDirectory(GetStoragePath(CmhcUploadFileTypes.QrSlides));

            var templateDir = GetTemplateDirectory();
            Directory.CreateDirectory(templateDir);

            _logger.LogInformation(
                "CMHC upload storage ready at {StoragePath}; templates at {TemplateDir}",
                storagePath,
                templateDir);
        }

        public async Task<string> SaveUploadAsync(
            Stream content,
            string sanitizedFileName,
            string uploadCategory,
            CancellationToken cancellationToken)
        {
            var storagePath = GetStoragePath(uploadCategory);
            Directory.CreateDirectory(storagePath);

            var uniqueName = CmhcFileNameHelper.ResolveUniqueFileName(storagePath, sanitizedFileName);
            var fullPath = Path.Combine(storagePath, uniqueName);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await content.CopyToAsync(fileStream, cancellationToken);

            _logger.LogInformation("Saved file upload {FileName} to {Path}", uniqueName, fullPath);
            return uniqueName;
        }

        public Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    $"CMHC template file was not found at '{templatePath}'.",
                    _options.TemplateFileName);
            }

            Stream stream = new FileStream(
                templatePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult((stream, _options.TemplateFileName));
        }

        public Task DeleteUploadAsync(
            string storedFileName,
            string uploadCategory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeName = CmhcFileNameHelper.SanitizeFileName(storedFileName);
            var fullPath = Path.Combine(GetStoragePath(uploadCategory), safeName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file upload {Path}", fullPath);
            }

            return Task.CompletedTask;
        }

        public Task<(Stream Stream, string FileName)> GetQrSlideAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeName = CmhcFileNameHelper.SanitizeFileName(fileName);
            var storagePath = GetStoragePath(CmhcUploadFileTypes.QrSlides);
            var fullPath = ResolveQrSlidePath(storagePath, safeName);
            if (fullPath is null)
            {
                throw new FileNotFoundException(
                    $"QR slide file '{safeName}' was not found in local qr_slides storage.",
                    safeName);
            }

            Stream stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult((stream, Path.GetFileName(fullPath)!));
        }

        public Task<(Stream Stream, string FileName)> GetLakehouseFilesPathAsync(
            string filesRelativePath,
            string? lakehouseId = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = filesRelativePath.Trim().Replace('\\', '/').Trim('/');
            if (normalizedPath.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("Lakehouse file path is invalid.", nameof(filesRelativePath));
            }

            var localRoot = Path.GetFullPath(Path.Combine(_contentRoot, "CmhcUploads", "Files"));
            var fullPath = Path.GetFullPath(Path.Combine(localRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(localRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Lakehouse file '{normalizedPath}' was not found in local Files mirror.",
                    normalizedPath);
            }

            Stream stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult((stream, Path.GetFileName(fullPath)!));
        }

        private static string? ResolveQrSlidePath(string storagePath, string safeName)
        {
            var exactPath = Path.Combine(storagePath, safeName);
            if (File.Exists(exactPath))
            {
                return exactPath;
            }

            if (!Directory.Exists(storagePath))
            {
                return null;
            }

            var match = Directory
                .EnumerateFiles(storagePath)
                .FirstOrDefault(path =>
                {
                    var name = Path.GetFileName(path);
                    return name.Equals(safeName, StringComparison.OrdinalIgnoreCase)
                        && HasQrSlideExtension(name);
                });

            return match;
        }

        private static bool HasQrSlideExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private string GetStoragePath(string? uploadCategory = null)
        {
            if (_resolvedStoragePaths.TryGetValue(CmhcUploadFileTypes.Normalize(uploadCategory), out var cached))
            {
                return cached;
            }

            var path = _options.LocalStoragePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(_contentRoot, path);
            }

            var category = CmhcUploadFileTypes.Normalize(uploadCategory);
            var relativePath = _options.GetUploadRelativePath(category);
            var resolved = Path.GetFullPath(Path.Combine(path, relativePath));
            _resolvedStoragePaths[category] = resolved;
            return resolved;
        }

        private string GetTemplateDirectory()
        {
            var siblingTemplates = Path.GetFullPath(
                Path.Combine(GetStoragePath(CmhcUploadFileTypes.CmhcExcel), "..", "templates"));

            var wwwrootTemplates = Path.Combine(_contentRoot, "wwwroot", "templates");
            return Directory.Exists(wwwrootTemplates) ? wwwrootTemplates : siblingTemplates;
        }

        private string ResolveTemplatePath()
        {
            var candidates = new[]
            {
                Path.Combine(_contentRoot, "wwwroot", "templates", _options.TemplateFileName),
                Path.GetFullPath(
                    Path.Combine(GetStoragePath(CmhcUploadFileTypes.CmhcExcel), "..", "templates", _options.TemplateFileName))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return candidates[0];
        }
    }
}
