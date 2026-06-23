using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace kingsightapi.Services
{
    public sealed class CmhcUploadService : ICmhcUploadService
    {
        private const string HistorySql = """
            select file_id,
                   filename,
                   uploaded_date,
                   uploaded_by
            from mort.CMHC_upload_historytbl
            order by uploaded_date desc
            """;

        private const string NextFileIdSql = """
            select isnull(max(file_id), 0) + 1
            from mort.CMHC_upload_historytbl
            """;

        private const string InsertSql = """
            insert into mort.CMHC_upload_historytbl (file_id, filename, uploaded_date, uploaded_by)
            values (@file_id, @filename, sysutcdatetime(), @uploaded_by)
            """;

        private const string GetInsertedRowSql = """
            select file_id,
                   filename,
                   uploaded_date,
                   uploaded_by
            from mort.CMHC_upload_historytbl
            where file_id = @file_id
            """;

        private readonly string _connectionString;
        private readonly ICmhcFileStorage _fileStorage;
        private readonly IUserService _userService;
        private readonly CmhcUploadOptions _options;
        private readonly ILogger<CmhcUploadService> _logger;

        public CmhcUploadService(
            IConfiguration configuration,
            ICmhcFileStorage fileStorage,
            IUserService userService,
            IOptions<CmhcUploadOptions> options,
            ILogger<CmhcUploadService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _fileStorage = fileStorage;
            _userService = userService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CmhcUploadHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken)
        {
            var rows = new List<CmhcUploadHistoryDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(HistorySql, connection);
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
                var row = await InsertHistoryRowAsync(storedFileName, uploadedByStorageGuid, cancellationToken);
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
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var nextIdCommand = new SqlCommand(NextFileIdSql, connection);
            var nextId = Convert.ToInt64(await nextIdCommand.ExecuteScalarAsync(cancellationToken));

            await using var insertCommand = new SqlCommand(InsertSql, connection);
            insertCommand.Parameters.AddWithValue("@file_id", nextId);
            insertCommand.Parameters.AddWithValue("@filename", filename);
            insertCommand.Parameters.Add(CreateUploadedByParameter("@uploaded_by", uploadedByStorageGuid));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var getCommand = new SqlCommand(GetInsertedRowSql, connection);
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

        private static CmhcUploadHistoryDto MapRow(SqlDataReader reader)
        {
            var uploadedByOrdinal = reader.GetOrdinal("uploaded_by");
            var uploadedByValue = reader.GetGuid(uploadedByOrdinal);
            var row = new CmhcUploadHistoryDto
            {
                FileId = Convert.ToInt64(reader.GetValue(reader.GetOrdinal("file_id"))),
                Filename = reader.GetString(reader.GetOrdinal("filename")),
                UploadedDate = reader.GetDateTime(reader.GetOrdinal("uploaded_date")),
                UploadedBy = uploadedByValue.ToString()
            };

            ApplyUploadedByDisplay(row, new Dictionary<int, UserDto>());
            return row;
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
