namespace kingsightapi.Services
{
    internal static class CmhcFileNameHelper
    {
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            var sanitized = Path.GetFileName(fileName.Trim());
            if (string.IsNullOrWhiteSpace(sanitized)
                || sanitized.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("File name is invalid.", nameof(fileName));
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return sanitized;
        }

        public static string ResolveUniqueFileName(string directory, string sanitizedFileName)
        {
            var targetPath = Path.Combine(directory, sanitizedFileName);
            if (!File.Exists(targetPath))
            {
                return sanitizedFileName;
            }

            var extension = Path.GetExtension(sanitizedFileName);
            var baseName = Path.GetFileNameWithoutExtension(sanitizedFileName);

            for (var i = 1; i < 10_000; i++)
            {
                var candidate = $"{baseName}_{i}{extension}";
                if (!File.Exists(Path.Combine(directory, candidate)))
                {
                    return candidate;
                }
            }

            throw new IOException("Unable to allocate a unique file name for the upload.");
        }

        public static bool HasAllowedExtension(string fileName, IReadOnlyList<string> allowedExtensions)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return allowedExtensions.Any(
                allowed => extension.Equals(allowed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
