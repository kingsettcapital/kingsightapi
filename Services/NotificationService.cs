using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface INotificationService
    {
        Task<IReadOnlyList<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<bool> MarkAsReadAsync(
            IReadOnlyList<long> notificationIds,
            CancellationToken cancellationToken = default);

        Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

        Task CreateRankingUpdateAsync(
            string loanCode,
            short? priorRanking,
            short? currentRanking,
            string updatedBy,
            CancellationToken cancellationToken = default);

        Task CreateDefaultDateUpdateAsync(
            string loanCode,
            DateTime? priorDefaultDate,
            DateTime? currentDefaultDate,
            string updatedBy,
            CancellationToken cancellationToken = default);

        Task CreateLtvReviewedAsync(
            string updatedBy,
            CancellationToken cancellationToken = default);
    }

    public sealed class NotificationService : INotificationService
    {
        private const string LoanAttributeAssignmentScreen = "Loan Attribute Assignment";
        private const string DefaultDateCaptureScreen = "Default Date Capture";
        private const string LtvValidationScreen = "LTV Validation";

        private readonly string _connectionString;
        private readonly string _notificationsTable;
        private readonly string _notificationMasterTable;
        private readonly ILogger<NotificationService> _logger;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);

        private bool _schemaProbed;
        private bool _tableAvailable;
        private bool _hasNotificationId;

        // Fabric Warehouse rejects multi-arg CHECKSUM; hash one concatenated key for legacy tables without notification_id.
        private const string SyntheticNotificationIdExpression = """
            abs(checksum(concat(
                isnull(notification_type, ''),
                char(1),
                isnull(notice, ''),
                char(1),
                isnull(updated_by, ''),
                char(1),
                isnull(convert(varchar(30), updated_date, 126), '1900-01-01'))))
            """;

        public NotificationService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            ILogger<NotificationService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _notificationsTable = tables.SubjectiveInput("notifications");
            _notificationMasterTable = tables.SubjectiveInput("notification_master");
            _logger = logger;
        }

        public async Task<IReadOnlyList<NotificationDto>> GetAllAsync(CancellationToken cancellationToken)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (!_tableAvailable)
            {
                return [];
            }

            var rows = new List<NotificationDto>();

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(BuildListSql(), connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(MapRow(reader));
                }
            }
            catch (SqlException ex) when (ex.Number is 102 or 208 or 3701 or 207 or 245)
            {
                _logger.LogWarning(ex, "notifications query failed (schema mismatch or unsupported SQL); returning empty list.");
                _tableAvailable = false;
                return [];
            }

            return rows;
        }

        public async Task<bool> MarkAsReadAsync(
            IReadOnlyList<long> notificationIds,
            CancellationToken cancellationToken)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (!_tableAvailable || notificationIds.Count == 0)
            {
                return false;
            }

            var ids = notificationIds.Where(id => id != 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                return false;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affected = 0;
            foreach (var id in ids)
            {
                await using var command = new SqlCommand(BuildMarkOneReadSql(), connection);
                command.Parameters.AddWithValue("@notification_id", id);
                affected += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return affected > 0;
        }

        public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (!_tableAvailable)
            {
                return 0;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                $"""
                update {_notificationsTable}
                set is_read = 1
                where isnull(is_read, 0) = 0
                """,
                connection);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task CreateRankingUpdateAsync(
            string loanCode,
            short? priorRanking,
            short? currentRanking,
            string updatedBy,
            CancellationToken cancellationToken)
        {
            if (!await IsTriggerActiveAsync(LoanAttributeAssignmentScreen, "Ranking", cancellationToken))
            {
                _logger.LogInformation(
                    "Skipped ranking notification for {LoanCode}; no active rule in notification_master for Loan Attribute Assignment / Ranking.",
                    loanCode);
                return;
            }

            if (priorRanking == currentRanking)
            {
                _logger.LogInformation(
                    "Skipped ranking notification for {LoanCode}; ranking unchanged ({Ranking}).",
                    loanCode,
                    currentRanking);
                return;
            }

            var priorLabel = priorRanking?.ToString() ?? "null";
            var currentLabel = currentRanking?.ToString() ?? "null";
            var notice =
                $"{loanCode.Trim()} has been updated from Ranking {priorLabel} to Ranking {currentLabel}";

            await InsertAsync("Ranking Update", notice, updatedBy, cancellationToken);
        }

        public async Task CreateDefaultDateUpdateAsync(
            string loanCode,
            DateTime? priorDefaultDate,
            DateTime? currentDefaultDate,
            string updatedBy,
            CancellationToken cancellationToken)
        {
            if (!await IsTriggerActiveAsync(DefaultDateCaptureScreen, "Default Date", cancellationToken))
            {
                return;
            }

            if (priorDefaultDate?.Date == currentDefaultDate?.Date)
            {
                return;
            }

            var priorLabel = FormatDateLabel(priorDefaultDate);
            var currentLabel = FormatDateLabel(currentDefaultDate);
            var notice =
                $"{loanCode.Trim()} Default Date has been updated from {priorLabel} to {currentLabel}";

            await InsertAsync("Default Date Update", notice, updatedBy, cancellationToken);
        }

        public async Task CreateLtvReviewedAsync(string updatedBy, CancellationToken cancellationToken)
        {
            if (!await IsTriggerActiveAsync(LtvValidationScreen, "Confirm LTV", cancellationToken))
            {
                _logger.LogInformation(
                    "Skipped LTV Reviewed notification; no active rule in notification_master for LTV Validation / Confirm LTV.");
                return;
            }

            var periodLabel = GetCurrentQuarterLabel(DateTime.UtcNow);
            var notice =
                $"The LTV for {periodLabel} has been Reviewed and Finalized and Reflected in Reporting";

            await InsertAsync("LTV Reviewed", notice, updatedBy, cancellationToken);
        }

        private async Task InsertAsync(
            string notificationType,
            string notice,
            string updatedBy,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(notice))
            {
                return;
            }

            await EnsureSchemaAsync(cancellationToken);
            if (!_tableAvailable)
            {
                _logger.LogWarning(
                    "Skipped creating notification because notifications table is unavailable.");
                return;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(
                    $"""
                    insert into {_notificationsTable}
                        (notification_type, notice, is_read, updated_by, updated_date)
                    values
                        (@notification_type, @notice, 0, @updated_by, @updated_date)
                    """,
                    connection);

                command.Parameters.AddWithValue("@notification_type", notificationType.Trim());
                command.Parameters.AddWithValue("@notice", notice.Trim());
                command.Parameters.AddWithValue("@updated_by", updatedBy.Trim());
                command.Parameters.AddWithValue("@updated_date", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation(
                    "Created notification {NotificationType} by {UpdatedBy}",
                    notificationType,
                    updatedBy);
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                _tableAvailable = false;
                _logger.LogWarning(ex, "notifications table unavailable; insert skipped.");
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to insert notification {NotificationType} into {Table}.",
                    notificationType,
                    _notificationsTable);
                throw;
            }
        }

        private async Task<bool> IsTriggerActiveAsync(
            string screenName,
            string screenAttribute,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            try
            {
                await EnsureNotificationMasterDefaultsAsync(connection, cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                _logger.LogWarning(
                    "notification_master table unavailable; allowing notification for {Screen}/{Attribute}.",
                    screenName,
                    screenAttribute);
                return true;
            }

            await using var command = new SqlCommand(
                $"""
                select top 1 1
                from {_notificationMasterTable}
                where screen_name = @screen_name
                  and screen_attribute = @screen_attribute
                  and isnull(is_active, 0) = 1
                """,
                connection);

            command.Parameters.AddWithValue("@screen_name", screenName);
            command.Parameters.AddWithValue("@screen_attribute", screenAttribute);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        // Insert Phase I defaults when a rule is missing (ranking may have been seeded before LTV).
        private async Task EnsureNotificationMasterDefaultsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await EnsureDefaultRuleAsync(
                connection,
                LoanAttributeAssignmentScreen,
                "Ranking",
                "loan_alias_relationship",
                "loan_ranking",
                cancellationToken);
            await EnsureDefaultRuleAsync(
                connection,
                DefaultDateCaptureScreen,
                "Default Date",
                "loan_alias_relationship",
                "default_date",
                cancellationToken);
            await EnsureDefaultRuleAsync(
                connection,
                LtvValidationScreen,
                "Confirm LTV",
                "loan_alias_relationship",
                "current_loan_to_value",
                cancellationToken);
        }

        private async Task EnsureDefaultRuleAsync(
            SqlConnection connection,
            string screenName,
            string screenAttribute,
            string tableName,
            string columnName,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(
                $"""
                if not exists (
                    select 1
                    from {_notificationMasterTable}
                    where screen_name = @screen_name
                      and screen_attribute = @screen_attribute)
                begin
                    insert into {_notificationMasterTable}
                        (role_id, screen_name, screen_attribute, table_name, column_name, is_active)
                    values
                        (null, @screen_name, @screen_attribute, @table_name, @column_name, 1)
                end
                """,
                connection);

            command.Parameters.AddWithValue("@screen_name", screenName);
            command.Parameters.AddWithValue("@screen_attribute", screenAttribute);
            command.Parameters.AddWithValue("@table_name", tableName);
            command.Parameters.AddWithValue("@column_name", columnName);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            await _schemaLock.WaitAsync(cancellationToken);
            try
            {
                if (_schemaProbed)
                {
                    return;
                }

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                try
                {
                    await using var command = new SqlCommand(
                        $"select top 0 notification_type from {_notificationsTable}",
                        connection);
                    await command.ExecuteReaderAsync(cancellationToken);
                    _tableAvailable = true;
                }
                catch (SqlException ex) when (ex.Number is 208 or 3701)
                {
                    _tableAvailable = false;
                    _logger.LogWarning(
                        ex,
                        "subjective_input.notifications is not available. Run Scripts/Create_subjective_input_notifications.sql.");
                }

                if (_tableAvailable)
                {
                    _hasNotificationId = await DimLoanColumnProbe.FindFirstAsync(
                        _connectionString,
                        _notificationsTable,
                        ["notification_id"],
                        cancellationToken) is not null;
                }

                _schemaProbed = true;
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private string BuildListSql()
        {
            var idSelect = _hasNotificationId
                ? "notification_id"
                : $"{SyntheticNotificationIdExpression} as notification_id";

            return $"""
                select {idSelect},
                       notification_type,
                       notice,
                       is_read,
                       updated_by,
                       updated_date
                from {_notificationsTable}
                order by updated_date desc, notification_type, notice
                """;
        }

        private string BuildMarkOneReadSql()
        {
            if (_hasNotificationId)
            {
                return $"""
                    update {_notificationsTable}
                    set is_read = 1
                    where notification_id = @notification_id
                      and isnull(is_read, 0) = 0
                    """;
            }

            return $"""
                update {_notificationsTable}
                set is_read = 1
                where {SyntheticNotificationIdExpression} = @notification_id
                  and isnull(is_read, 0) = 0
                """;
        }

        private static NotificationDto MapRow(SqlDataReader reader)
        {
            var isReadOrdinal = reader.GetOrdinal("is_read");
            var isRead = !reader.IsDBNull(isReadOrdinal) && Convert.ToInt32(reader.GetValue(isReadOrdinal)) != 0;

            var updatedDateOrdinal = reader.GetOrdinal("updated_date");
            DateTime updatedDate = DateTime.UtcNow;
            if (!reader.IsDBNull(updatedDateOrdinal))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(updatedDateOrdinal), DateTimeKind.Utc);
            }

            return new NotificationDto
            {
                NotificationId = Convert.ToInt64(reader.GetValue(reader.GetOrdinal("notification_id"))),
                NotificationType = reader.IsDBNull(reader.GetOrdinal("notification_type"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("notification_type")).Trim(),
                Notice = reader.IsDBNull(reader.GetOrdinal("notice"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("notice")).Trim(),
                IsRead = isRead,
                UpdatedBy = reader.IsDBNull(reader.GetOrdinal("updated_by"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("updated_by")).Trim(),
                UpdatedDate = updatedDate,
            };
        }

        private static string FormatDateLabel(DateTime? value) =>
            value.HasValue ? value.Value.ToString("MM/dd/yyyy") : "null";

        private static string GetCurrentQuarterLabel(DateTime utcNow)
        {
            var quarter = (utcNow.Month - 1) / 3 + 1;
            return $"Q{quarter} {utcNow.Year}";
        }
    }
}
