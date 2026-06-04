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
        private string? _resolvedStoragePath;

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
            var storagePath = GetStoragePath();
            Directory.CreateDirectory(storagePath);

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
            CancellationToken cancellationToken)
        {
            var storagePath = GetStoragePath();
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

            _logger.LogInformation("Saved CMHC upload file {FileName} to {Path}", uniqueName, fullPath);
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

        public Task DeleteUploadAsync(string storedFileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var safeName = CmhcFileNameHelper.SanitizeFileName(storedFileName);
            var fullPath = Path.Combine(GetStoragePath(), safeName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted CMHC upload file {Path}", fullPath);
            }

            return Task.CompletedTask;
        }

        private string GetStoragePath()
        {
            if (_resolvedStoragePath is not null)
            {
                return _resolvedStoragePath;
            }

            var path = _options.LocalStoragePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(_contentRoot, path);
            }

            _resolvedStoragePath = Path.GetFullPath(path);
            return _resolvedStoragePath;
        }

        private string GetTemplateDirectory()
        {
            var siblingTemplates = Path.GetFullPath(
                Path.Combine(GetStoragePath(), "..", "templates"));

            var wwwrootTemplates = Path.Combine(_contentRoot, "wwwroot", "templates");
            return Directory.Exists(wwwrootTemplates) ? wwwrootTemplates : siblingTemplates;
        }

        private string ResolveTemplatePath()
        {
            var candidates = new[]
            {
                Path.Combine(_contentRoot, "wwwroot", "templates", _options.TemplateFileName),
                Path.GetFullPath(
                    Path.Combine(GetStoragePath(), "..", "templates", _options.TemplateFileName))
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
