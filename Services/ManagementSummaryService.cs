using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IManagementSummaryService
    {
        Task<IReadOnlyList<ManagementSummaryRowDto>> GetSummaryAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LoanDetailReportRowDto>> GetLoanDetailsAsync(
            int loanAliasKey,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<ManagementSummaryDashboardDto> GetDashboardAsync(
            ManagementSummaryDashboardQuery query,
            CancellationToken cancellationToken = default);

        Task<LoanDetailReportDashboardDto> GetLoanDetailReportAsync(
            int loanAliasKey,
            LoanDetailReportQuery query,
            CancellationToken cancellationToken = default);
    }

    public sealed partial class ManagementSummaryService : IManagementSummaryService
    {
        private static readonly string[] ParentLoanKeyColumnCandidates =
            ["parent_loan_key", "loan_parent_key", "parent_key"];

        private static readonly string[] ExposureColumnCandidates =
            ["exposure", "loan_exposure", "outstanding_balance", "collateral"];

        private static readonly string[] DimLoanLtvColumnCandidates =
            ["ai_ltv", "loan_ltv", "ltv", "ltv_percent"];

        private static readonly string[] DefaultDateColumnCandidates =
            ["default_date", "loan_default_date"];

        private static readonly string[] DefaultStatusColumnCandidates =
            ["default_subjective_status", "default_status_subjective"];

        private const string LoanEligibleWhere = """
            where l.is_current = 1
              and (l.is_leaf = 1 or l.is_leaf is null)
              and l.loan_alias_key is not null
            """;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblDimInvestor;
        private readonly string _tblInvestorAliasMaster;
        private readonly string _tblDimStatus;
        private readonly string _tblLtvValidation;
        private readonly string _tblTaxArrears;
        private readonly string _tblCmhcDefaultWatchlist;
        private readonly string _vwLoanAttributes;
        private readonly string _fnExposure;
        private readonly string _fnManagementDetailsLoanPortfolio;
        private readonly string _fnManagementDetailTopbarSummary;
        private readonly string _fnManagementDetailReport;
        private readonly string _fnManagementDetailPropertyStats;
        private readonly string _fnManagementDetailInterestReserve;
        private readonly string _fnManagementDetailExposureByInvestor;
        private readonly string _fnManagementDetailExposureComposition;
        private readonly string _fnManagementDetailTaxArrearsByYear;
        private readonly string _fnManagementDetailKeyDates;
        private readonly string _fnManagementDetailInterestOverLife;
        private readonly string _fnManagementSummaryPortfolioKpis;
        private readonly string _fnManagementSummaryLoanAlias;
        private readonly string _fnManagementSummaryRiskDistribution;
        private readonly string _fnManagementSummaryTop5Exposure;
        private readonly string _fnManagementSummaryExposureBreakdown;
        private readonly string _fnManagementSummaryInvestorSummary;
        private readonly string _fnManagementSummarySponsorSummary;
        private readonly string _fnManagementSummaryExposureAnalysis;
        private readonly string _tblSubjectiveLoanAliasMaster;
        private readonly ILogger<ManagementSummaryService> _logger;
        private string? _loanStatusKeyColumn;
        private string? _parentLoanKeyColumn;
        private bool? _parentLoanKeyColumnResolved;
        private string? _exposureColumn;
        private bool? _exposureColumnResolved;
        private string? _dimLoanLtvColumn;
        private bool? _dimLoanLtvColumnResolved;
        private string? _defaultDateColumn;
        private bool? _defaultDateColumnResolved;
        private string? _defaultStatusColumn;
        private bool? _defaultStatusColumnResolved;
        private bool? _ltvTableAvailable;

        public ManagementSummaryService(
            IConfiguration configuration,
            ILogger<ManagementSummaryService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _tables = tables;
            _tblDimLoan = tables.Mort("dim_loan");
            _tblLoanAliasMaster = tables.Mort("loan_alias_master");
            _tblDimInvestor = tables.Mort("dim_investor");
            _tblInvestorAliasMaster = tables.Mort("investor_alias_master");
            _tblDimStatus = tables.Shared("dim_status");
            _tblLtvValidation = tables.Mort("ltv_validation");
            _tblTaxArrears = tables.Mort("tax_arrears");
            _tblCmhcDefaultWatchlist = tables.ExternalFiles("cmhc_default");
            _vwLoanAttributes = tables.Mortgage("vw_loan_attributes");
            _fnExposure = tables.MortgageObject("fn_exposure");
            _fnManagementDetailsLoanPortfolio =
                tables.MortgageObject("fn_management_detail_loan_portfolio");
            _fnManagementDetailTopbarSummary =
                tables.MortgageObject("fn_management_detail_topbar_summary");
            _fnManagementDetailReport =
                tables.MortgageObject("fn_management_detail_report");
            _fnManagementDetailPropertyStats =
                tables.MortgageObject("fn_management_detail_property_stats");
            _fnManagementDetailInterestReserve =
                tables.MortgageObject("fn_management_detail_interest_reserve");
            _fnManagementDetailExposureByInvestor =
                tables.MortgageObject("fn_management_detail_exposure_by_investor");
            _fnManagementDetailExposureComposition =
                tables.MortgageObject("fn_management_detail_exposure_composition");
            _fnManagementDetailTaxArrearsByYear =
                tables.MortgageObject("fn_management_detail_tax_arrears_by_year");
            _fnManagementDetailKeyDates =
                tables.MortgageObject("fn_management_detail_key_dates");
            _fnManagementDetailInterestOverLife =
                tables.MortgageObject("fn_management_detail_interest_over_life");
            _fnManagementSummaryPortfolioKpis =
                tables.MortgageObject("fn_management_summary_portfolio_kpis");
            _fnManagementSummaryLoanAlias =
                tables.MortgageObject("fn_management_summary_loan_alias");
            _fnManagementSummaryRiskDistribution =
                tables.MortgageObject("fn_management_summary_risk_distribution");
            _fnManagementSummaryTop5Exposure =
                tables.MortgageObject("fn_management_summary_top5_exposure");
            _fnManagementSummaryExposureBreakdown =
                tables.MortgageObject("fn_management_summary_exposure_breakdown");
            _fnManagementSummaryInvestorSummary =
                tables.MortgageObject("fn_management_summary_investor_summary");
            _fnManagementSummarySponsorSummary =
                tables.MortgageObject("fn_management_summary_sponsor_summary");
            _fnManagementSummaryExposureAnalysis =
                tables.MortgageObject("fn_management_summary_exposure_analysis");
            _tblSubjectiveLoanAliasMaster = tables.SubjectiveInput("loan_alias_master");
        }

        public async Task<IReadOnlyList<ManagementSummaryRowDto>> GetSummaryAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var normalizedAliasIds = NormalizeAliasIds(loanAliasIds);
            var statusFilter = await BuildStatusFilterAsync(statuses, cancellationToken);
            var sql = await BuildSummarySqlAsync(normalizedAliasIds, statusFilter, cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, normalizedAliasIds);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<ManagementSummaryRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapSummaryRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} management summary rows for {AliasCount} loan alias filter(s).",
                rows.Count,
                normalizedAliasIds?.Count ?? 0);

            return rows;
        }

        public async Task<IReadOnlyList<LoanDetailReportRowDto>> GetLoanDetailsAsync(
            int loanAliasKey,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var statusFilter = await BuildStatusFilterAsync(statuses, cancellationToken);
            var sql = await BuildDetailSqlAsync([loanAliasKey], statusFilter, cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, [loanAliasKey]);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<LoanDetailReportRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapDetailRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} loan detail rows for loan alias {LoanAliasKey}.",
                rows.Count,
                loanAliasKey);

            return rows;
        }

        private async Task<LoanStatusFilter> BuildStatusFilterAsync(
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken)
        {
            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            if (statusFilter.HasFilter)
            {
                var loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
                if (string.IsNullOrEmpty(loanStatusKeyColumn))
                {
                    throw new InvalidOperationException("Status filter requires loan_status_key on mort.dim_loan.");
                }
            }

            return statusFilter;
        }

        private async Task<string> BuildSummarySqlAsync(
            IReadOnlyList<int>? loanAliasIds,
            LoanStatusFilter statusFilter,
            CancellationToken cancellationToken)
        {
            await EnsureLtvTableAvailableAsync(cancellationToken);
            var exposureColumn = await GetExposureColumnAsync(cancellationToken);
            var dimLoanLtvColumn = await GetDimLoanLtvColumnAsync(cancellationToken);
            var defaultDateColumn = await GetDefaultDateColumnAsync(cancellationToken);
            var defaultStatusColumn = await GetDefaultStatusColumnAsync(cancellationToken);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
            }

            var exposureExpr = string.IsNullOrEmpty(exposureColumn)
                ? "cast(0 as decimal(18, 2))"
                : $"isnull(l.{exposureColumn}, 0)";

            var ltvExpr = BuildLtvExpression(dimLoanLtvColumn);
            var defaultDateExpr = string.IsNullOrEmpty(defaultDateColumn)
                ? "cast(null as datetime2)"
                : $"l.{defaultDateColumn}";
            var defaultStatusExpr = string.IsNullOrEmpty(defaultStatusColumn)
                ? "cast(null as varchar(100))"
                : $"l.{defaultStatusColumn}";

            var sql = new StringBuilder();
            sql.AppendLine("""
                select l.loan_alias_key,
                       loan_alias_name = max(isnull(m.loan_alias_name, '')),
                       ranking = min(l.loan_ranking),
                       investor_alias_name = max(isnull(iam.investor_alias_name, '')),
                       loan_count = count(*),
                """);
            sql.AppendLine($"       total_exposure = sum({exposureExpr}),");
            sql.AppendLine("       security_value = max(m.security_value),");
            sql.AppendLine($"       avg_ltv = avg({ltvExpr}),");
            sql.AppendLine($"       default_status = max({defaultStatusExpr}),");
            sql.AppendLine($"       default_date = max({defaultDateExpr})");
            sql.Append(await BuildFromClauseAsync(cancellationToken));
            sql.Append(LoanEligibleWhere);
            AppendLoanAliasFilter(sql, loanAliasIds);

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.AppendLine(" group by l.loan_alias_key");
            sql.AppendLine(" order by max(m.loan_alias_name), l.loan_alias_key");
            return sql.ToString();
        }

        private async Task<string> BuildDetailSqlAsync(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            CancellationToken cancellationToken)
        {
            await EnsureLtvTableAvailableAsync(cancellationToken);
            var parentLoanKeyColumn = await GetParentLoanKeyColumnAsync(cancellationToken);
            var exposureColumn = await GetExposureColumnAsync(cancellationToken);
            var dimLoanLtvColumn = await GetDimLoanLtvColumnAsync(cancellationToken);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
            }

            var parentLoanIdSelect = string.IsNullOrEmpty(parentLoanKeyColumn)
                ? "parent_loan_id = isnull(l.dummy_loan_link, '')"
                : "parent_loan_id = isnull(parent.loan_code, isnull(l.dummy_loan_link, ''))";

            var exposureSelect = string.IsNullOrEmpty(exposureColumn)
                ? "cast(null as decimal(18, 2)) as exposure"
                : $"l.{exposureColumn} as exposure";

            var ltvSelect = string.IsNullOrEmpty(dimLoanLtvColumn)
                ? "ltv = coalesce(lv.ltv, lv.ai_ltv)"
                : $"ltv = coalesce(lv.ltv, lv.ai_ltv, l.{dimLoanLtvColumn})";

            var sql = new StringBuilder();
            sql.AppendLine("select l.loan_key,");
            sql.AppendLine($"       {parentLoanIdSelect},");
            sql.AppendLine("""
                       child_loan_id = l.loan_code,
                       l.loan_desc,
                       investor_alias_name = isnull(iam.investor_alias_name, ''),
                       m.security_value,
                """);
            sql.AppendLine($"       {exposureSelect},");
            sql.AppendLine($"       {ltvSelect}");
            sql.Append(await BuildFromClauseAsync(cancellationToken, parentLoanKeyColumn));
            sql.Append(LoanEligibleWhere);
            AppendLoanAliasFilter(sql, loanAliasIds);

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by m.loan_alias_name, l.loan_code");
            return sql.ToString();
        }

        private async Task<string> BuildFromClauseAsync(
            CancellationToken cancellationToken,
            string? parentLoanKeyColumn = null)
        {
            parentLoanKeyColumn ??= await GetParentLoanKeyColumnAsync(cancellationToken);
            await EnsureLtvTableAvailableAsync(cancellationToken);

            if (!string.IsNullOrEmpty(parentLoanKeyColumn))
            {
                return $"""

                    from {_tblDimLoan} l
                    left join {_tblDimLoan} parent
                        on l.{parentLoanKeyColumn} = parent.loan_key
                       and parent.is_current = 1
                    left join {_tblLoanAliasMaster} m
                        on l.loan_alias_key = m.loan_alias_id
                    left join {_tblDimInvestor} inv
                        on l.investor_key = inv.investor_key
                       and inv.is_current = 1
                    left join {_tblInvestorAliasMaster} iam
                        on inv.investor_alias_key = iam.investor_alias_id
                    left join {_tblLtvValidation} lv
                        on l.loan_key = lv.loan_key

                    """;
            }

            return $"""

                from {_tblDimLoan} l
                left join {_tblLoanAliasMaster} m
                    on l.loan_alias_key = m.loan_alias_id
                left join {_tblDimInvestor} inv
                    on l.investor_key = inv.investor_key
                   and inv.is_current = 1
                left join {_tblInvestorAliasMaster} iam
                    on inv.investor_alias_key = iam.investor_alias_id
                left join {_tblLtvValidation} lv
                    on l.loan_key = lv.loan_key

                """;
        }

        private static string BuildLtvExpression(string? dimLoanLtvColumn) =>
            string.IsNullOrEmpty(dimLoanLtvColumn)
                ? "coalesce(lv.ltv, lv.ai_ltv)"
                : $"coalesce(lv.ltv, lv.ai_ltv, l.{dimLoanLtvColumn})";

        private static void AppendLoanAliasFilter(StringBuilder sql, IReadOnlyList<int>? loanAliasIds)
        {
            if (loanAliasIds is null or { Count: 0 })
            {
                return;
            }

            sql.Append(" and l.loan_alias_key in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');
        }

        private static IReadOnlyList<int>? NormalizeAliasIds(IReadOnlyList<int>? loanAliasIds)
        {
            if (loanAliasIds is null or { Count: 0 })
            {
                return null;
            }

            return loanAliasIds.Where(id => id > 0).Distinct().ToList();
        }

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int>? loanAliasIds)
        {
            if (loanAliasIds is null)
            {
                return;
            }

            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private async Task EnsureLtvTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_ltvTableAvailable == true)
            {
                return;
            }

            var probeSql = $"select top 0 loan_key from {_tblLtvValidation}";
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(probeSql, connection);
                await command.ExecuteReaderAsync(cancellationToken);
                _ltvTableAvailable = true;
            }
            catch (SqlException)
            {
                _ltvTableAvailable = false;
            }
        }

        private async Task<string?> GetParentLoanKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (_parentLoanKeyColumnResolved == true)
            {
                return _parentLoanKeyColumn;
            }

            _parentLoanKeyColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                ParentLoanKeyColumnCandidates,
                cancellationToken);

            _parentLoanKeyColumnResolved = true;
            return _parentLoanKeyColumn;
        }

        private async Task<string?> GetExposureColumnAsync(CancellationToken cancellationToken)
        {
            if (_exposureColumnResolved == true)
            {
                return _exposureColumn;
            }

            _exposureColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                ExposureColumnCandidates,
                cancellationToken);

            _exposureColumnResolved = true;
            return _exposureColumn;
        }

        private async Task<string?> GetDimLoanLtvColumnAsync(CancellationToken cancellationToken)
        {
            if (_dimLoanLtvColumnResolved == true)
            {
                return _dimLoanLtvColumn;
            }

            _dimLoanLtvColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                DimLoanLtvColumnCandidates,
                cancellationToken);

            _dimLoanLtvColumnResolved = true;
            return _dimLoanLtvColumn;
        }

        private async Task<string?> GetDefaultDateColumnAsync(CancellationToken cancellationToken)
        {
            if (_defaultDateColumnResolved == true)
            {
                return _defaultDateColumn;
            }

            _defaultDateColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                DefaultDateColumnCandidates,
                cancellationToken);

            _defaultDateColumnResolved = true;
            return _defaultDateColumn;
        }

        private async Task<string?> GetDefaultStatusColumnAsync(CancellationToken cancellationToken)
        {
            if (_defaultStatusColumnResolved == true)
            {
                return _defaultStatusColumn;
            }

            _defaultStatusColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                DefaultStatusColumnCandidates,
                cancellationToken);

            _defaultStatusColumnResolved = true;
            return _defaultStatusColumn;
        }

        private async Task<string> GetLoanStatusKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))
            {
                return _loanStatusKeyColumn;
            }

            _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                _connectionString,
                _tblDimLoan,
                cancellationToken);

            return _loanStatusKeyColumn;
        }

        private static ManagementSummaryRowDto MapSummaryRow(SqlDataReader reader) =>
            new()
            {
                LoanAliasKey = GetInt32(reader, "loan_alias_key"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                Ranking = GetNullableInt32(reader, "ranking"),
                InvestorAliasName = GetString(reader, "investor_alias_name"),
                LoanCount = GetInt32(reader, "loan_count"),
                TotalExposure = GetNullableDecimal(reader, "total_exposure"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                AvgLtv = GetNullableDecimal(reader, "avg_ltv"),
                DefaultStatus = GetNullableString(reader, "default_status"),
                DefaultDate = GetNullableDateTime(reader, "default_date")
            };

        private static LoanDetailReportRowDto MapDetailRow(SqlDataReader reader) =>
            new()
            {
                LoanKey = GetInt64(reader, "loan_key"),
                ParentLoanId = GetString(reader, "parent_loan_id"),
                ChildLoanId = GetString(reader, "child_loan_id"),
                Description = GetString(reader, "loan_desc"),
                InvestorAliasName = GetString(reader, "investor_alias_name"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Exposure = GetNullableDecimal(reader, "exposure"),
                Ltv = GetNullableDecimal(reader, "ltv")
            };

        private static int GetInt32(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0
                : Convert.ToInt32(reader.GetValue(reader.GetOrdinal(name)));

        private static long GetInt64(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0L
                : Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

        private static string GetString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal)
                ? string.Empty
                : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static string? GetNullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var text = Convert.ToString(reader.GetValue(ordinal));
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static int? GetNullableInt32(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
            }
        }

        private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            try
            {
                return Convert.ToDecimal(value);
            }
            catch (Exception)
            {
                return decimal.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
            }
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                DateTime dateTime => dateTime,
                DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                _ => DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : null
            };
        }
    }
}
