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
            IEnumerable<string> existing = Directory.Exists(directory)
                ? Directory.GetFiles(directory).Select(Path.GetFileName).OfType<string>()
                : [];

            return ResolveUniqueFileName(existing, sanitizedFileName);
        }

        public static string ResolveUniqueFileName(IEnumerable<string> existingFileNames, string sanitizedFileName)
        {
            var existing = existingFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(sanitizedFileName))
            {
                return sanitizedFileName;
            }

            var extension = Path.GetExtension(sanitizedFileName);
            var baseName = Path.GetFileNameWithoutExtension(sanitizedFileName);

            for (var i = 1; i < 10_000; i++)
            {
                var candidate = $"{baseName}_{i}{extension}";
                if (!existing.Contains(candidate))
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
