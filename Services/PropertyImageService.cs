using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace kingsightapi.Services
{
    public interface IPropertyImageService
    {
        Task<PropertyImageDto?> GetActiveAsync(long loanAliasId, CancellationToken cancellationToken);

        Task<(Stream Stream, string ContentType)?> OpenActiveAsync(long loanAliasId, CancellationToken cancellationToken);

        Task<PropertyImageDto> UploadAsync(
            long loanAliasId,
            IFormFile file,
            string auditUser,
            CancellationToken cancellationToken);

        Task<bool> DeactivateAsync(long loanAliasId, string auditUser, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Property photo per loan alias. Metadata in subjective_input.loan_alias_property_image,
    /// file in the CMHC upload storage under <c>external_files/property_images/{loanAliasId}</c>.
    /// </summary>
    public sealed class PropertyImageService : IPropertyImageService
    {
        private readonly string _connectionString;
        private readonly string _imageTable;
        private readonly string _loanAliasMasterTable;
        private readonly ICmhcFileStorage _fileStorage;
        private readonly CmhcUploadOptions _options;
        private readonly ILogger<PropertyImageService> _logger;

        public PropertyImageService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            ICmhcFileStorage fileStorage,
            IOptions<CmhcUploadOptions> options,
            ILogger<PropertyImageService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");

            _imageTable = tables.SubjectiveInput("loan_alias_property_image");
            _loanAliasMasterTable = tables.SubjectiveInput("loan_alias_master");
            _fileStorage = fileStorage;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<PropertyImageDto?> GetActiveAsync(long loanAliasId, CancellationToken cancellationToken)
        {
            var sql = $"""
                select top 1 property_image_id, loan_alias_id, file_name, original_file_name, content_type,
                       file_size_bytes, created_by, created_datetime
                from {_imageTable}
                where loan_alias_id = @loan_alias_id and is_active = 1
                order by property_image_id desc
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new PropertyImageDto
            {
                PropertyImageId = reader.GetInt64(0),
                LoanAliasId = reader.GetInt64(1),
                FileName = reader.GetString(2),
                OriginalFileName = reader.GetString(3),
                ContentType = reader.GetString(4),
                FileSizeBytes = reader.GetInt64(5),
                UploadedBy = reader.IsDBNull(6) ? null : reader.GetString(6),
                UploadedDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            };
        }

        public async Task<(Stream Stream, string ContentType)?> OpenActiveAsync(
            long loanAliasId,
            CancellationToken cancellationToken)
        {
            var active = await GetActiveAsync(loanAliasId, cancellationToken);
            if (active is null)
            {
                return null;
            }

            try
            {
                var stream = await _fileStorage.OpenPropertyImageAsync(loanAliasId, active.FileName, cancellationToken);
                return (stream, ContentTypeFor(Path.GetExtension(active.FileName)));
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Active property image row exists but file is missing for loan alias {LoanAliasId}", loanAliasId);
                return null;
            }
        }

        public async Task<PropertyImageDto> UploadAsync(
            long loanAliasId,
            IFormFile file,
            string auditUser,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new CmhcUploadValidationException("Image file is required.");
            }

            if (file.Length > _options.MaxPropertyImageBytes)
            {
                throw new CmhcUploadValidationException(
                    $"Image is too large. Maximum size is {_options.MaxPropertyImageBytes / 1024 / 1024} MB.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            var originalFileName = Path.GetFileName(file.FileName ?? string.Empty);
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (!CmhcFileNameHelper.HasAllowedExtension(originalFileName, _options.PropertyImageAllowedExtensions))
            {
                throw new CmhcUploadValidationException(
                    $"Only {string.Join(", ", _options.PropertyImageAllowedExtensions)} images are allowed.");
            }

            if (!await LoanAliasExistsAsync(loanAliasId, cancellationToken))
            {
                throw new CmhcUploadValidationException("Loan alias not found.", StatusCodes.Status404NotFound);
            }

            var auditDtm = DateTime.UtcNow;
            var contentType = ContentTypeFor(extension);
            var sanitizedFileName = CmhcFileNameHelper.SanitizeFileName(
                $"{loanAliasId}_{auditDtm:yyyyMMdd_HHmmss}{extension}");

            await using var stream = file.OpenReadStream();
            var storedFileName = await _fileStorage.SavePropertyImageAsync(
                stream,
                loanAliasId,
                sanitizedFileName,
                cancellationToken);

            try
            {
                var sql = $"""
                    update {_imageTable}
                    set is_active = 0, updated_by = @audit_user, updated_datetime = @audit_dtm
                    where loan_alias_id = @loan_alias_id and is_active = 1;

                    declare @property_image_id bigint = (select isnull(max(property_image_id), 0) + 1 from {_imageTable});

                    insert into {_imageTable}
                        (property_image_id, loan_alias_id, file_name, original_file_name, content_type, file_size_bytes,
                         is_active, created_by, created_datetime, updated_by, updated_datetime)
                    values
                        (@property_image_id, @loan_alias_id, @file_name, @original_file_name, @content_type, @file_size_bytes,
                         1, @audit_user, @audit_dtm, @audit_user, @audit_dtm);

                    select @property_image_id;
                    """;

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
                command.Parameters.AddWithValue("@file_name", storedFileName);
                command.Parameters.AddWithValue("@original_file_name", originalFileName);
                command.Parameters.AddWithValue("@content_type", contentType);
                command.Parameters.AddWithValue("@file_size_bytes", file.Length);
                command.Parameters.AddWithValue("@audit_user", auditUser);
                command.Parameters.AddWithValue("@audit_dtm", auditDtm);

                var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Uploaded property image {FileName} for loan alias {LoanAliasId}",
                    storedFileName,
                    loanAliasId);

                return new PropertyImageDto
                {
                    PropertyImageId = id,
                    LoanAliasId = loanAliasId,
                    FileName = storedFileName,
                    OriginalFileName = originalFileName,
                    ContentType = contentType,
                    FileSizeBytes = file.Length,
                    UploadedBy = auditUser,
                    UploadedDate = auditDtm
                };
            }
            catch
            {
                try
                {
                    await _fileStorage.DeletePropertyImageAsync(loanAliasId, storedFileName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to roll back stored property image {FileName}", storedFileName);
                }

                throw;
            }
        }

        public async Task<bool> DeactivateAsync(long loanAliasId, string auditUser, CancellationToken cancellationToken)
        {
            var sql = $"""
                update {_imageTable}
                set is_active = 0, updated_by = @audit_user, updated_datetime = @audit_dtm
                where loan_alias_id = @loan_alias_id and is_active = 1
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            command.Parameters.AddWithValue("@audit_user", auditUser);
            command.Parameters.AddWithValue("@audit_dtm", DateTime.UtcNow);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private async Task<bool> LoanAliasExistsAsync(long loanAliasId, CancellationToken cancellationToken)
        {
            var sql = $"""
                select count(1) from {_loanAliasMasterTable} where loan_alias_id = @loan_alias_id
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
