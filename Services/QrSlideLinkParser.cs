namespace kingsightapi.Services
{
    internal static class QrSlideLinkParser
    {
        private static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];

        private static readonly string[] FabricSelectedPathQueryKeys =
            ["selectedPath", "selectedpath", "path"];

        /// <summary>
        /// Fabric portal link resolved to a lakehouse file under <c>Files/</c>.
        /// </summary>
        public sealed record FabricQrSlideReference(string LakehouseId, string FilesRelativePath);

        /// <summary>
        /// Resolves a Fabric portal URL to a lakehouse file path (AI-rendered QR pages).
        /// </summary>
        public static FabricQrSlideReference? TryExtractFabricQrSlide(string? link)
        {
            if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            var lakehouseId = TryExtractLakehouseId(uri);
            if (string.IsNullOrWhiteSpace(lakehouseId))
            {
                return null;
            }

            foreach (var value in ReadQueryValues(uri.Query))
            {
                var normalized = NormalizeLakehouseFilesPath(value);
                if (normalized is not null)
                {
                    return new FabricQrSlideReference(lakehouseId, normalized);
                }
            }

            return null;
        }

        /// <summary>
        /// OneLake path under lakehouse <c>Files/</c> (without lakehouse id).
        /// </summary>
        public static string? TryExtractFabricSelectedPath(string? link) =>
            TryExtractFabricQrSlide(link)?.FilesRelativePath;

        /// <summary>
        /// Resolves a stored qr_slide_link to a slide file name under qr_slides upload storage.
        /// </summary>
        public static string? TryExtractFileName(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            var trimmed = link.Trim();

            if (!trimmed.Contains('/', StringComparison.Ordinal)
                && !trimmed.Contains('\\', StringComparison.Ordinal))
            {
                return TrySanitizeSlideFileName(trimmed);
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
            {
                var fromPath = ExtractSlideFileFromPath(absoluteUri.AbsolutePath);
                if (fromPath is not null)
                {
                    return fromPath;
                }

                foreach (var queryValue in ReadQueryValues(absoluteUri.Query))
                {
                    var fromQuery = ExtractSlideFileFromPath(queryValue)
                        ?? TrySanitizeSlideFileName(Path.GetFileName(queryValue));
                    if (fromQuery is not null)
                    {
                        return fromQuery;
                    }
                }

                var fromDecodedUrl = ExtractSlideFileFromPath(Uri.UnescapeDataString(trimmed));
                if (fromDecodedUrl is not null)
                {
                    return fromDecodedUrl;
                }
            }

            var pathFile = ExtractSlideFileFromPath(trimmed);
            if (pathFile is not null)
            {
                return pathFile;
            }

            var decoded = Uri.UnescapeDataString(trimmed);
            if (!string.Equals(decoded, trimmed, StringComparison.Ordinal))
            {
                pathFile = ExtractSlideFileFromPath(decoded);
                if (pathFile is not null)
                {
                    return pathFile;
                }
            }

            return TrySanitizeSlideFileName(
                Path.GetFileName(trimmed.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static IReadOnlyList<string> BuildFileNameCandidates(string fileName)
        {
            var candidates = new List<string>();
            AddCandidate(candidates, fileName);

            var extension = Path.GetExtension(fileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return candidates;
            }

            if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, $"{baseName}.pdf");
            }

            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, $"{baseName}.png");
            }

            return candidates;
        }

        private static IEnumerable<string> ReadQueryValues(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                yield break;
            }

            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pieces = part.Split('=', 2);
                if (pieces.Length != 2)
                {
                    continue;
                }

                if (!FabricSelectedPathQueryKeys.Any(
                        key => key.Equals(pieces[0], StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var decoded = FullyDecodeUriComponent(pieces[1]);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    yield return decoded;
                }
            }
        }

        private static string FullyDecodeUriComponent(string value)
        {
            var current = value;
            for (var i = 0; i < 5; i++)
            {
                var decoded = Uri.UnescapeDataString(current);
                if (string.Equals(decoded, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = decoded;
            }

            return current;
        }

        private static string? TryExtractLakehouseId(Uri uri)
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!segments[i].Equals("lakehouses", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = segments[i + 1].Trim();
                return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
            }

            return null;
        }

        private static string? NormalizeLakehouseFilesPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = FullyDecodeUriComponent(value.Trim()).Replace('\\', '/').Trim('/');
            if (normalized.Contains("..", StringComparison.Ordinal))
            {
                return null;
            }

            if (normalized.StartsWith("Files/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["Files/".Length..];
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var fileName = Path.GetFileName(normalized);
            if (!HasAllowedExtension(fileName))
            {
                return null;
            }

            return normalized;
        }

        private static void AddCandidate(List<string> candidates, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            if (candidates.Any(existing => existing.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            candidates.Add(fileName);
        }

        private static string? ExtractSlideFileFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = segments.Length - 1; i >= 0; i--)
            {
                var candidate = TrySanitizeSlideFileName(Uri.UnescapeDataString(segments[i]));
                if (candidate is not null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string? TrySanitizeSlideFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (!HasAllowedExtension(trimmed))
            {
                return null;
            }

            try
            {
                return CmhcFileNameHelper.SanitizeFileName(trimmed);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool HasAllowedExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return AllowedExtensions.Any(
                allowed => extension.Equals(allowed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
