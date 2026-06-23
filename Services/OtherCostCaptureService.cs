using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IOtherCostCaptureService
    {
        Task<IReadOnlyList<OtherCostCaptureDto>> GetAsync(
            int loanAliasId,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            OtherCostCaptureBatchUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class OtherCostCaptureService : IOtherCostCaptureService
    {
        private const string ListSqlBase = """
            select l.loan_key,
                   l.loan_code,
                   l.loan_desc,
                   loan_alias_name = isnull(m.loan_alias_name, ''),
                   l.outstanding_invoice_value,
                   l.estimated_realization_value,
                   l.cost_to_complete_value,
                   l.user_updated_by,
                   l.user_updated_date
            from mort.dim_loan l
            left join mort.loan_alias_master m
                on l.loan_alias_key = m.loan_alias_id
            where l.is_current = 1
              and (l.is_leaf = 1 or l.is_leaf is null)
              and l.loan_alias_key = @loan_alias_id
            """;

        private const string UpdateSql = """
            update mort.dim_loan
            set outstanding_invoice_value = @outstanding_invoice_value,
                estimated_realization_value = @estimated_realization_value,
                cost_to_complete_value = @cost_to_complete_value,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where loan_key = @loan_key
              and is_current = 1
              and (is_leaf = 1 or is_leaf is null)
            """;

        private readonly string _connectionString;
        private readonly ILogger<OtherCostCaptureService> _logger;
        private string? _loanStatusKeyColumn;

        public OtherCostCaptureService(IConfiguration configuration, ILogger<OtherCostCaptureService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<OtherCostCaptureDto>> GetAsync(
            int loanAliasId,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            var sql = new StringBuilder(ListSqlBase);

            if (statusFilter.HasFilter)
            {
                var loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter);
            }

            sql.AppendLine();
            sql.Append(" order by l.loan_code");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql.ToString(), connection);
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<OtherCostCaptureDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var ordinals = GetOrdinals(reader);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader, ordinals));
            }

            _logger.LogInformation(
                "Retrieved {Count} other cost capture rows for loan alias {LoanAliasId}.",
                rows.Count,
                loanAliasId);

            return rows;
        }

        public async Task<bool> UpdateAsync(
            OtherCostCaptureBatchUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                await using var command = new SqlCommand(UpdateSql, connection);
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@outstanding_invoice_value",
                    loan.OutstandingInvoices.HasValue ? loan.OutstandingInvoices.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@estimated_realization_value",
                    loan.EstRealizationCosts.HasValue ? loan.EstRealizationCosts.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@cost_to_complete_value",
                    loan.CostToComplete.HasValue ? loan.CostToComplete.Value : DBNull.Value);
                command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} other cost capture loan rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No other cost capture loan rows updated.");
            return false;
        }

        private async Task<string> GetLoanStatusKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))
            {
                return _loanStatusKeyColumn;
            }

            _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                _connectionString,
                cancellationToken);

            _logger.LogInformation(
                "Using mort.dim_loan.{Column} for other cost capture status filter.",
                _loanStatusKeyColumn);

            return _loanStatusKeyColumn;
        }

        private static OtherCostCaptureDto MapRow(
            SqlDataReader reader,
            (int Key, int Code, int Desc, int AliasName, int Outstanding, int EstRealization, int CostToComplete, int UpdatedBy, int UpdatedDate) ordinals)
        {
            return new OtherCostCaptureDto
            {
                LoanKey = reader.IsDBNull(ordinals.Key)
                    ? 0L
                    : Convert.ToInt64(reader.GetValue(ordinals.Key)),
                LoanId = reader.IsDBNull(ordinals.Code)
                    ? string.Empty
                    : reader.GetString(ordinals.Code),
                Description = reader.IsDBNull(ordinals.Desc)
                    ? string.Empty
                    : reader.GetString(ordinals.Desc),
                LoanAliasName = reader.IsDBNull(ordinals.AliasName)
                    ? string.Empty
                    : reader.GetString(ordinals.AliasName),
                OutstandingInvoices = GetNullableDecimal(reader, ordinals.Outstanding),
                EstRealizationCosts = GetNullableDecimal(reader, ordinals.EstRealization),
                CostToComplete = GetNullableDecimal(reader, ordinals.CostToComplete),
                UserUpdatedBy = reader.IsDBNull(ordinals.UpdatedBy)
                    ? string.Empty
                    : reader.GetString(ordinals.UpdatedBy),
                UserUpdatedDate = reader.IsDBNull(ordinals.UpdatedDate)
                    ? null
                    : reader.GetDateTime(ordinals.UpdatedDate)
            };
        }

        private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));

        private static (int Key, int Code, int Desc, int AliasName, int Outstanding, int EstRealization, int CostToComplete, int UpdatedBy, int UpdatedDate) GetOrdinals(
            SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("loan_key"),
                reader.GetOrdinal("loan_code"),
                reader.GetOrdinal("loan_desc"),
                reader.GetOrdinal("loan_alias_name"),
                reader.GetOrdinal("outstanding_invoice_value"),
                reader.GetOrdinal("estimated_realization_value"),
                reader.GetOrdinal("cost_to_complete_value"),
                reader.GetOrdinal("user_updated_by"),
                reader.GetOrdinal("user_updated_date"));
        }
    }
}
