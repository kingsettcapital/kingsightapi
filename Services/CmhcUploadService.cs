using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace kingsightapi.Services
{
    public sealed class CmhcUploadService : ICmhcUploadService
    {
        private readonly string _cmhcUploadHistoryTable;
        private readonly string NextFileIdSql;

        private readonly string _connectionString;
        private readonly ICmhcFileStorage _fileStorage;
        private readonly IUserService _userService;
        private readonly CmhcUploadOptions _options;
        private readonly ILogger<CmhcUploadService> _logger;

        private bool _schemaProbed;
        private string? _asOfDateColumn;

        public CmhcUploadService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            ICmhcFileStorage fileStorage,
            IUserService userService,
            IOptions<CmhcUploadOptions> options,
            ILogger<CmhcUploadService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");

            var cmhcUploadHistory = tables.Mort("CMHC_upload_historytbl");
            _cmhcUploadHistoryTable = cmhcUploadHistory;

            NextFileIdSql = $"""
                select isnull(max(file_id), 0) + 1
                from {cmhcUploadHistory}
                """;

            _fileStorage = fileStorage;
            _userService = userService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CmhcUploadHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken)
        {
            await EnsureSchemaAsync(cancellationToken);

            var rows = new List<CmhcUploadHistoryDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildHistorySql(), connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            await EnrichUploadedByDisplayAsync(rows, cancellationToken);

            _logger.LogInformation("Retrieved {Count} CMHC upload history rows.", rows.Count);
            return rows;
        }

        public async Task<CmhcUploadHistoryDto> UploadAsync(
            IFormFile file,
            string fileName,
            int uploadedByUserId,
            string fileType,
            DateOnly asOfDate,
            CancellationToken cancellationToken)
        {
            if (uploadedByUserId <= 0)
            {
                throw new CmhcUploadValidationException("uploadedByUserId must be a positive integer.");
            }

            var uploadCategory = CmhcUploadFileTypes.Normalize(fileType);
            var uploadedByStorageGuid = UploadUserIdentityCodec.ToStorageGuid(uploadedByUserId);
            ValidateUpload(file, fileName, uploadCategory);

            var sanitizedFileName = CmhcFileNameHelper.SanitizeFileName(fileName);

            await using var stream = file.OpenReadStream();
            var storedFileName = await _fileStorage.SaveUploadAsync(
                stream,
                sanitizedFileName,
                uploadCategory,
                cancellationToken);

            try
            {
                var row = await InsertHistoryRowAsync(
                    storedFileName,
                    uploadedByStorageGuid,
                    asOfDate,
                    cancellationToken);
                await EnrichUploadedByDisplayAsync([row], cancellationToken);
                return row;
            }
            catch
            {
                try
                {
                    await _fileStorage.DeleteUploadAsync(storedFileName, uploadCategory, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to roll back stored CMHC file {FileName}", storedFileName);
                }

                throw;
            }
        }

        public Task<(Stream Stream, string FileName)> GetTemplateAsync(CancellationToken cancellationToken) =>
            _fileStorage.GetTemplateAsync(cancellationToken);

        public async Task<(Stream Stream, string FileName)> GetQrSlidePreviewAsync(
            string link,
            CancellationToken cancellationToken = default)
        {
            var fabricReference = QrSlideLinkParser.TryExtractFabricQrSlide(link);
            if (fabricReference is not null)
            {
                try
                {
                    _logger.LogInformation(
                        "Serving QR slide preview for link '{Link}' from lakehouse {LakehouseId}/Files/{Path}.",
                        link,
                        fabricReference.LakehouseId,
                        fabricReference.FilesRelativePath);

                    return await _fileStorage.GetLakehouseFilesPathAsync(
                        fabricReference.FilesRelativePath,
                        fabricReference.LakehouseId,
                        cancellationToken);
                }
                catch (FileNotFoundException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Lakehouse QR slide path {LakehouseId}/Files/{Path} was not found; falling back to qr_slides storage.",
                        fabricReference.LakehouseId,
                        fabricReference.FilesRelativePath);
                }
            }

            var fabricPath = fabricReference?.FilesRelativePath;

            var fileName = QrSlideLinkParser.TryExtractFileName(link);
            if (fileName is null)
            {
                throw new FileNotFoundException(
                    "Could not determine a QR slide file name from the link. " +
                    "Store a Fabric selectedPath URL, a .pdf/.png file name, or upload to QR Slides.");
            }

            var candidates = QrSlideLinkParser.BuildFileNameCandidates(fileName);
            FileNotFoundException? lastError = null;

            foreach (var candidate in candidates)
            {
                try
                {
                    _logger.LogInformation(
                        "Serving QR slide preview for link '{Link}' as qr_slides file '{FileName}'.",
                        link,
                        candidate);

                    return await _fileStorage.GetQrSlideAsync(candidate, cancellationToken);
                }
                catch (FileNotFoundException ex)
                {
                    lastError = ex;
                    _logger.LogDebug(ex, "QR slide candidate '{Candidate}' was not found.", candidate);
                }
            }

            throw new FileNotFoundException(
                fabricPath is not null
                    ? $"QR slide was not found at lakehouse Files/{fabricPath} or in qr_slides storage."
                    : $"QR slide file '{fileName}' was not found in qr_slides storage. " +
                      "Upload the matching PDF or PNG via Mortgage → File Upload → QR Slides.",
                fileName,
                lastError);
        }

        private void ValidateUpload(IFormFile file, string fileName, string uploadCategory)
        {
            if (file is null || file.Length == 0)
            {
                throw new CmhcUploadValidationException("Upload file is required.");
            }

            if (file.Length > _options.MaxFileSizeBytes)
            {
                throw new CmhcUploadValidationException(
                    $"File size exceeds the maximum allowed size of {_options.MaxFileSizeBytes} bytes.",
                    statusCode: 413);
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new CmhcUploadValidationException("fileName is required.");
            }

            var nameForExtensionCheck = CmhcFileNameHelper.SanitizeFileName(fileName);
            var allowedExtensions = _options.GetAllowedExtensions(uploadCategory);

            if (!CmhcFileNameHelper.HasAllowedExtension(nameForExtensionCheck, allowedExtensions))
            {
                throw new CmhcUploadValidationException(
                    "File extension is not allowed. Allowed types: "
                    + string.Join(", ", allowedExtensions));
            }
        }

        private async Task<CmhcUploadHistoryDto> InsertHistoryRowAsync(
            string filename,
            Guid uploadedByStorageGuid,
            DateOnly asOfDate,
            CancellationToken cancellationToken)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var nextIdCommand = new SqlCommand(NextFileIdSql, connection);
            var nextId = Convert.ToInt64(await nextIdCommand.ExecuteScalarAsync(cancellationToken));

            await using var insertCommand = new SqlCommand(BuildInsertSql(), connection);
            insertCommand.Parameters.AddWithValue("@file_id", nextId);
            insertCommand.Parameters.AddWithValue("@filename", filename);
            insertCommand.Parameters.AddWithValue("@uploaded_date", DateTime.UtcNow);
            insertCommand.Parameters.Add(CreateUploadedByParameter("@uploaded_by", uploadedByStorageGuid));
            if (_asOfDateColumn is not null)
            {
                insertCommand.Parameters.AddWithValue("@as_of_date", asOfDate.ToDateTime(TimeOnly.MinValue));
            }

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var getCommand = new SqlCommand(BuildGetInsertedRowSql(), connection);
            getCommand.Parameters.AddWithValue("@file_id", nextId);

            await using var reader = await getCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("CMHC upload insert did not return a row.");
            }

            var row = MapRow(reader);
            _logger.LogInformation(
                "Inserted CMHC upload history row {FileId} for {FileName} by user {UploadedByUserId}",
                row.FileId,
                row.Filename,
                row.UploadedByUserId);

            return row;
        }

        private async Task EnrichUploadedByDisplayAsync(
            IList<CmhcUploadHistoryDto> rows,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var users = await _userService.GetAllAsync(cancellationToken);
            var usersById = users.ToDictionary(user => user.UserId);

            foreach (var row in rows)
            {
                ApplyUploadedByDisplay(row, usersById);
            }
        }

        private static void ApplyUploadedByDisplay(
            CmhcUploadHistoryDto row,
            IReadOnlyDictionary<int, UserDto> usersById)
        {
            if (!Guid.TryParse(row.UploadedBy, out var storageGuid))
            {
                row.UploadedByName ??= row.UploadedBy;
                return;
            }

            if (storageGuid == Guid.Empty)
            {
                row.UploadedByName = "system";
                return;
            }

            var userId = UploadUserIdentityCodec.TryParseUserId(storageGuid);
            row.UploadedByUserId = userId;

            if (userId.HasValue && usersById.TryGetValue(userId.Value, out var user))
            {
                row.UploadedByName = FormatUserDisplayName(user);
                return;
            }

            row.UploadedByName ??= userId.HasValue ? $"User {userId.Value}" : row.UploadedBy;
        }

        private static string FormatUserDisplayName(UserDto user)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? user.Email : name;
        }

        private static SqlParameter CreateUploadedByParameter(string name, Guid uploadedByUserId) =>
            new(name, SqlDbType.UniqueIdentifier) { Value = uploadedByUserId };

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _asOfDateColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _cmhcUploadHistoryTable,
                ["as_of_date"],
                cancellationToken);
            _schemaProbed = true;
        }

        private string BuildHistorySql()
        {
            var asOfSelect = _asOfDateColumn is not null ? $", [{_asOfDateColumn}]" : string.Empty;
            return $"""
                select file_id,
                       filename,
                       uploaded_date,
                       uploaded_by{asOfSelect}
                from {_cmhcUploadHistoryTable}
                order by uploaded_date desc
                """;
        }

        private string BuildInsertSql()
        {
            if (_asOfDateColumn is not null)
            {
                return $"""
                    insert into {_cmhcUploadHistoryTable} (file_id, filename, uploaded_date, uploaded_by, [{_asOfDateColumn}])
                    values (@file_id, @filename, @uploaded_date, @uploaded_by, @as_of_date)
                    """;
            }

            return $"""
                insert into {_cmhcUploadHistoryTable} (file_id, filename, uploaded_date, uploaded_by)
                values (@file_id, @filename, @uploaded_date, @uploaded_by)
                """;
        }

        private string BuildGetInsertedRowSql()
        {
            var asOfSelect = _asOfDateColumn is not null ? $", [{_asOfDateColumn}]" : string.Empty;
            return $"""
                select file_id,
                       filename,
                       uploaded_date,
                       uploaded_by{asOfSelect}
                from {_cmhcUploadHistoryTable}
                where file_id = @file_id
                """;
        }

        private CmhcUploadHistoryDto MapRow(SqlDataReader reader)
        {
            var uploadedByOrdinal = reader.GetOrdinal("uploaded_by");
            var uploadedByValue = reader.GetGuid(uploadedByOrdinal);
            var row = new CmhcUploadHistoryDto
            {
                FileId = Convert.ToInt64(reader.GetValue(reader.GetOrdinal("file_id"))),
                Filename = reader.GetString(reader.GetOrdinal("filename")),
                UploadedDate = DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("uploaded_date")),
                    DateTimeKind.Utc),
                UploadedBy = uploadedByValue.ToString(),
                AsOfDate = TryGetOptionalDateColumn(reader, _asOfDateColumn)
            };

            ApplyUploadedByDisplay(row, new Dictionary<int, UserDto>());
            return row;
        }

        private static DateTime? TryGetOptionalDateColumn(SqlDataReader reader, string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return null;
            }

            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }
    }

    public sealed class CmhcUploadValidationException : Exception
    {
        public CmhcUploadValidationException(string message, int statusCode = 400)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}

