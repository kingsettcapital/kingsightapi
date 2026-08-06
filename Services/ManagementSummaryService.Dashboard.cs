using System.Collections.Concurrent;
using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    public sealed partial class ManagementSummaryService
    {
        private static readonly string[] PrincipalColumnCandidates =
            ["principal_balance", "outstanding_balance", "exposure", "loan_exposure", "collateral"];

        private static readonly string[] AccrualPostedDateColumnCandidates =
            ["accrual_posted_date", "as_at_date", "reporting_date"];

        private static readonly string[] FundingStatusKeyColumnCandidates =
            ["funding_status_key", "loan_funding_status_key"];

        private static readonly string[] InterestRateColumnCandidates =
            ["interest_rate", "loan_rate", "rate"];

        private static readonly string[] SponsorColumnCandidates =
            ["sponsor_name", "borrower_name", "sponsor"];

        private static readonly string[] PropertyAddressColumnCandidates =
            ["property_address", "property_location", "address"];

        private static readonly string[] PropertyTypeColumnCandidates =
            ["property_type", "loan_property_type"];

        private static readonly string[] LoanTypeColumnCandidates =
            ["loan_type", "loan_product_type"];

        private static readonly string[] OutstandingInterestColumnCandidates =
            ["outstanding_interest", "os_interest", "loan_outstanding_interest"];

        private static readonly string[] AccruedInterestColumnCandidates =
            ["accrued_interest", "loan_accrued_interest"];

        private static readonly string[] LateInterestColumnCandidates =
            ["late_interest", "loan_late_interest"];

        private static readonly string[] InterestDisbursedColumnCandidates =
            ["interest_disbursed", "loan_interest_disbursed"];

        private static readonly string[] InterestNotDisbursedColumnCandidates =
            ["interest_not_disbursed", "loan_interest_not_disbursed"];

        private static readonly string[] DefaultInterestColumnCandidates =
            ["default_interest", "def_interest", "loan_default_interest"];

        private static readonly string[] InterestAdjustmentColumnCandidates =
            ["interest_adjustment", "int_adjustment", "int_adj"];

        private static readonly string[] InterestAdvanceColumnCandidates =
            ["interest_advance", "int_advance", "interest_adv"];

        private static readonly string[] OutstandingInvoiceColumnCandidates =
            ["outstanding_invoice", "outstanding_invoice_value", "outstanding_invoices"];

        private static readonly string[] EstimatedRealizationColumnCandidates =
            ["estimated_realization_costs", "estimated_realization_value", "est_realization_costs"];

        private static readonly string[] CostToCompleteColumnCandidates =
            ["cost_to_complete", "cost_to_complete_value"];

        private static readonly string[] MonthsInArrearsColumnCandidates =
            ["months_in_arrears", "loan_months_in_arrears"];

        private static readonly string[] TimesNsfdColumnCandidates =
            ["times_nsfd", "nsf_count", "loan_times_nsfd"];

        private static readonly string[] InterestReserveColumnCandidates =
            ["interest_reserve", "loan_interest_reserve"];

        private static readonly string[] InterestReserveBalanceColumnCandidates =
            ["interest_reserve_balance", "loan_interest_reserve_balance"];

        private static readonly string[] SmfInterestStatusColumnCandidates =
            ["smf_interest_status", "interest_status_smf"];

        private static readonly string[] MlpInterestStatusColumnCandidates =
            ["mlp_interest_status", "interest_status_mlp"];

        private static readonly string[] MaturityDateColumnCandidates =
            ["maturity_date", "loan_maturity_date"];

        private static readonly string[] ExitPlanColumnCandidates =
            ["subjective_exit_plan", "exit_plan", "default_exit_plan"];

        private static readonly string[] ExitDateColumnCandidates =
            ["subjective_exit_date", "exit_date", "default_exit_date"];

        private static readonly string[] WatchlistIssueColumnCandidates =
            ["cmhc_issue", "default_issue", "subjective_issue"];

        private static readonly string[] WatchlistConclusionColumnCandidates =
            ["cmhc_conclusion", "default_conclusion", "subjective_conclusion"];

        private static readonly string[] WatchlistStatusUpdateColumnCandidates =
            ["cmhc_status_update", "status_update", "default_status_update"];

        private static readonly string[] WatchlistDscrColumnCandidates =
            ["dscr", "loan_dscr", "debt_service_coverage"];

        private static readonly string[] WatchlistMissedColumnCandidates =
            ["cmhc_missed_payments", "missed_payments", "payments_missed"];

        private static readonly string[] WatchlistStatusColumnCandidates =
            ["cmhc_watchlist_status", "watchlist_status", "default_subjective_status"];

        private const string DefaultedStatusName = "Defaulted";

        private DashboardColumnMap? _dashboardColumns;
        private bool? _taxArrearsTableAvailable;
        private bool? _watchlistTableAvailable;
        private readonly ConcurrentDictionary<string, ManagementSummaryFilterOptionsDto> _filterOptionsCache = new(StringComparer.Ordinal);
        private IReadOnlyDictionary<string, int>? _loanAliasKeyLookup;
        private readonly SemaphoreSlim _loanAliasKeyLookupLock = new(1, 1);

        public async Task<ManagementSummaryDashboardDto> GetDashboardAsync(
            ManagementSummaryDashboardQuery query,
            CancellationToken cancellationToken = default)
        {
            var fundingStatus = ResolveFundingStatusDescription(query.Statuses);

            // Keep Fabric round-trips minimal: only queries that cannot be derived from alias rows.
            // LTV risk / top 5 / sponsor / exposure breakdown are computed in-memory from alias data.
            var kpisTask = LoadDashboardKpisAndBreakdownAsync(query.AsOfDate, fundingStatus, cancellationToken);
            var aliasTask = LoadDashboardAliasRowsAsync(query.AsOfDate, fundingStatus, cancellationToken);
            var watchlistTask = TryLoadWatchlistTableRowsAsync(null, cancellationToken);
            var filterOptionsTask = LoadMortgageViewFilterOptionsAsync(fundingStatus, cancellationToken);
            var investorTask = LoadDashboardInvestorSummaryAsync(query.AsOfDate, fundingStatus, cancellationToken);
            var exposureAnalysisTask = LoadDashboardExposureAnalysisAsync(query.AsOfDate, fundingStatus, cancellationToken);

            await Task.WhenAll(
                kpisTask,
                aliasTask,
                watchlistTask,
                filterOptionsTask,
                investorTask,
                exposureAnalysisTask);

            var kpisAndBreakdown = await kpisTask;
            var aliasRows = ApplyDashboardFilters(await aliasTask, query);
            var watchlistRows = DeduplicateWatchlistRows(await watchlistTask ?? []);
            var filterOptions = await filterOptionsTask;
            var investorSlices = await investorTask;
            var exposureAnalysisRows = ApplyExposureAnalysisFilters(await exposureAnalysisTask, query);

            var charts = BuildDashboardChartsFromAliasRows(
                aliasRows,
                investorSlices,
                kpisAndBreakdown.ExposureBreakdown);

            var watchlistDates = watchlistRows
                .Select(row => row.ReportDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();
            var watchlistAsAt = watchlistDates.Count == 0 ? (DateTime?)null : watchlistDates.Max();

            _logger.LogInformation(
                "Management summary dashboard for {AsOfDate}: {LoanCount} loans, {AliasCount} alias rows, {WatchlistCount} watchlist rows, {ExposureAnalysisCount} exposure analysis rows.",
                query.AsOfDate,
                kpisAndBreakdown.Kpis.NumberOfLoans,
                aliasRows.Count,
                watchlistRows.Count,
                exposureAnalysisRows.Count);

            return new ManagementSummaryDashboardDto
            {
                AsOfDate = query.AsOfDate,
                ReportPeriodLabel = BuildReportPeriodLabel(query.AsOfDate),
                Kpis = kpisAndBreakdown.Kpis,
                OutstandingInterest = kpisAndBreakdown.OutstandingInterest,
                LoanAliasRows = aliasRows,
                ExposureAnalysisRows = exposureAnalysisRows,
                WatchlistRows = watchlistRows,
                WatchlistAsAt = watchlistAsAt,
                FilterOptions = filterOptions,
                ChartsPhase2 = charts
            };
        }

        private static string? ResolveFundingStatusDescription(IReadOnlyList<string>? statuses)
        {
            // Filter vw_loan_attributes.funding_status_description using shared.dim_status
            // status_code (e.g. DEFAULT). UI may send status_name or status_code.
            if (statuses is null or { Count: 0 })
            {
                return null;
            }

            if (statuses.Any(status => status.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var token = statuses
                .Select(status => status?.Trim())
                .FirstOrDefault(status => !string.IsNullOrWhiteSpace(status));
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var normalized = token.ToUpperInvariant();
            return normalized switch
            {
                "ALL" => null,
                "IN DEFAULT" or "DEFAULTED" => "DEFAULT",
                "PERFORMING" => "FUNDED",
                // status_name uppercases to status_code for Unfunded/Funded/Default/Repaid
                _ => normalized
            };
        }

        private static string AppendFundingStatusFilter(string sql, string? fundingStatus, string columnExpression)
        {
            if (string.IsNullOrEmpty(fundingStatus))
            {
                return sql;
            }

            return sql + $"""

                where {columnExpression} = @funding_status
                """;
        }

        private static void AddFundingStatusParameter(SqlCommand command, string? fundingStatus)
        {
            if (!string.IsNullOrEmpty(fundingStatus))
            {
                command.Parameters.AddWithValue("@funding_status", fundingStatus);
            }
        }

        private async Task<(
            ManagementSummaryKpisDto Kpis,
            ManagementSummaryOutstandingInterestDto OutstandingInterest,
            IReadOnlyList<ChartSliceDto> ExposureBreakdown)>
            LoadDashboardKpisAndBreakdownAsync(
                DateOnly asOfDate,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var sql = AppendFundingStatusFilter(
                $"""
                select
                    count(distinct v.loan_code) as loan_count,
                    sum(a.principal_balance) as total_outstanding_balance,
                    avg(v.ltv) as average_ltv,
                    (sum(case when v.funding_status_description = 'DEFAULT' then a.principal_balance else 0 end)
                        / nullif(sum(a.principal_balance), 0)) * 100 as percentage_of_fundings,
                    sum(a.interest_disbursed) as interest_disbursed,
                    sum(a.interest_not_disbursed) as interest_not_disbursed,
                    sum(a.outstanding_loan_interest) as total_outstanding_interest,
                    sum(a.outstanding_late_interest) as total_late_interest,
                    sum(a.exposure) as exposure,
                    sum(a.accrued) as accrued,
                    sum(isnull(a.tax_arrears, 0)) as tax_arrear,
                    sum(a.monthly_interest_adjustment_amount) as interest_adjustment,
                    sum(isnull(a.outstanding_invoices, 0) + isnull(a.est_realization_costs, 0) + isnull(a.cost_to_complete, 0)) as other_cost
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                """,
                fundingStatus,
                "v.funding_status_description");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (
                    new ManagementSummaryKpisDto(),
                    new ManagementSummaryOutstandingInterestDto(),
                    Array.Empty<ChartSliceDto>());
            }

            var averageLtv = GetNullableDecimal(reader, "average_ltv");
            var percentOfFundings = GetNullableDecimal(reader, "percentage_of_fundings");
            var outstandingInterest = GetNullableDecimal(reader, "total_outstanding_interest") ?? 0m;
            var lateInterest = GetNullableDecimal(reader, "total_late_interest") ?? 0m;
            var totalExposure = GetNullableDecimal(reader, "exposure") ?? 0m;
            var components = new (string Label, decimal Value)[]
            {
                ("Outstanding Interest", outstandingInterest),
                ("Accrued", GetNullableDecimal(reader, "accrued") ?? 0m),
                ("Late Interest", lateInterest),
                ("Tax Arrears", GetNullableDecimal(reader, "tax_arrear") ?? 0m),
                ("Interest Adjustment", GetNullableDecimal(reader, "interest_adjustment") ?? 0m),
                ("Other", GetNullableDecimal(reader, "other_cost") ?? 0m)
            };
            var denominator = totalExposure > 0
                ? totalExposure
                : components.Sum(component => component.Value);
            var breakdown = components
                .Where(component => component.Value != 0m)
                .Select(component => new ChartSliceDto
                {
                    Label = component.Label,
                    Value = component.Value,
                    SharePercent = denominator > 0
                        ? Math.Round(component.Value / denominator * 100m, 1)
                        : null
                })
                .ToList();

            return (
                new ManagementSummaryKpisDto
                {
                    NumberOfLoans = GetInt32(reader, "loan_count"),
                    TotalOutstandingBalance = GetNullableDecimal(reader, "total_outstanding_balance") ?? 0m,
                    AverageLtv = averageLtv.HasValue ? Math.Round(averageLtv.Value, 2) : null,
                    PercentOfFundings = percentOfFundings.HasValue ? Math.Round(percentOfFundings.Value, 2) : null,
                    MaxLtv = null
                },
                new ManagementSummaryOutstandingInterestDto
                {
                    InterestDisbursed = GetNullableDecimal(reader, "interest_disbursed") ?? 0m,
                    InterestNotDisbursed = GetNullableDecimal(reader, "interest_not_disbursed") ?? 0m,
                    TotalOutstandingInterest = outstandingInterest,
                    TotalLateInterest = lateInterest
                },
                breakdown);
        }

        private static ManagementSummaryChartsPhase2Dto BuildDashboardChartsFromAliasRows(
            IReadOnlyList<LoanAliasSummaryRowDto> aliasRows,
            IReadOnlyList<ChartSliceDto> investorSummary,
            IReadOnlyList<ChartSliceDto> exposureBreakdown)
        {
            var totalExposure = aliasRows.Sum(row => row.TotalExposure);

            var ltvRiskDistribution = aliasRows
                .GroupBy(row => row.Risk)
                .Select(group =>
                {
                    var exposure = group.Sum(row => row.TotalExposure);
                    return new ChartSliceDto
                    {
                        Label = group.Key,
                        Value = exposure,
                        Count = group.Count(),
                        SharePercent = totalExposure > 0
                            ? Math.Round(exposure / totalExposure * 100m, 1)
                            : null
                    };
                })
                .OrderByDescending(slice => slice.Value)
                .ToList();

            var top5Exposures = aliasRows
                .OrderByDescending(row => row.TotalExposure)
                .Take(5)
                .Select(row => new ChartSliceDto
                {
                    Label = row.LoanAlias,
                    Value = row.TotalExposure,
                    SharePercent = totalExposure > 0
                        ? Math.Round(row.TotalExposure / totalExposure * 100m, 1)
                        : null
                })
                .ToList();

            var sponsorSummary = aliasRows
                .GroupBy(row => string.IsNullOrWhiteSpace(row.Sponsor) ? "(Unknown)" : row.Sponsor!)
                .Select(group =>
                {
                    var exposure = group.Sum(row => row.TotalExposure);
                    var ltvs = group.Where(row => row.Ltv.HasValue).Select(row => row.Ltv!.Value).ToList();
                    return new ChartSliceDto
                    {
                        Label = group.Key,
                        Value = exposure,
                        Count = group.Count(),
                        AverageLtv = ltvs.Count > 0 ? Math.Round(ltvs.Average(), 2) : null,
                        SharePercent = totalExposure > 0
                            ? Math.Round(exposure / totalExposure * 100m, 1)
                            : null
                    };
                })
                .Where(slice => slice.Value > 0)
                .OrderByDescending(slice => slice.AverageLtv ?? decimal.MinValue)
                .ToList();

            return new ManagementSummaryChartsPhase2Dto
            {
                LtvRiskDistribution = ltvRiskDistribution,
                Top5Exposures = top5Exposures,
                ExposureBreakdown = exposureBreakdown,
                ExposureAnalysis = [],
                InvestorSummary = investorSummary,
                SponsorSummary = sponsorSummary
            };
        }

        private async Task<(ManagementSummaryKpisDto Kpis, ManagementSummaryOutstandingInterestDto OutstandingInterest)>
            LoadDashboardKpisAsync(
                DateOnly asOfDate,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var combined = await LoadDashboardKpisAndBreakdownAsync(asOfDate, fundingStatus, cancellationToken);
            return (combined.Kpis, combined.OutstandingInterest);
        }

        private async Task<List<LoanAliasSummaryRowDto>> LoadDashboardAliasRowsAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "where funding_status_description = @funding_status";
            var joinStatusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "and v.funding_status_description = @funding_status";

            // Pre-aggregate text fields once (join), avoid correlated string_agg per alias row.
            var sql = $"""
                with distinct_interest_status as (
                    select
                        loan_alias_name,
                        string_agg(interest_status, ', ') as interest_status_alias
                    from (
                        select distinct
                            loan_alias_name,
                            case
                                when investor_alias_name in ('SMF', 'MLP') and date_interest_turned_off is not null
                                    then concat(investor_alias_name, ': Accruing')
                                when investor_alias_name in ('SMF', 'MLP') and date_interest_turned_off is null
                                    then investor_alias_name
                                else null
                            end as interest_status
                        from {_vwLoanAttributes}
                        {statusPredicate}
                    ) d
                    where interest_status is not null
                    group by loan_alias_name
                ),
                distinct_sponsor as (
                    select
                        loan_alias_name,
                        string_agg(sponsor, ', ') as sponsor
                    from (
                        select distinct loan_alias_name, sponsor
                        from {_vwLoanAttributes}
                        {statusPredicate}
                    ) d
                    where sponsor is not null
                    group by loan_alias_name
                ),
                distinct_exit as (
                    select
                        loan_alias_name,
                        string_agg(exit_plan, ', ') as ext
                    from (
                        select distinct loan_alias_name, exit_plan
                        from {_vwLoanAttributes}
                        {statusPredicate}
                    ) d
                    where exit_plan is not null
                    group by loan_alias_name
                )
                select
                    v.loan_alias_name,
                    s.sponsor,
                    min(case when v.default_date is null then v.loan_term_default_date else v.default_date end) as default_date,
                    min(v.maturity_date) as maturity_date,
                    i.interest_status_alias,
                    min(v.units_loan_alias) as units,
                    min(v.security_value_loan_alias) as security,
                    e.ext,
                    sum(a.principal_balance) as principal,
                    sum(a.outstanding_loan_interest) as outstanding_interest,
                    sum(a.accrued) as accrued,
                    sum(a.outstanding_late_interest) as late_interest,
                    sum(isnull(a.tax_arrears, 0)) as tax_arrear,
                    sum(a.monthly_interest_adjustment_amount) as interest_adjustment,
                    sum(isnull(a.outstanding_invoices, 0) + isnull(a.est_realization_costs, 0) + isnull(a.cost_to_complete, 0)) as other_cost,
                    sum(a.exposure) as total_exposure,
                    avg(v.ltv) as ltv,
                    case
                        when avg(v.ltv) < 50 then 'Low'
                        when avg(v.ltv) between 50 and 75 then 'Moderate'
                        when avg(v.ltv) between 75 and 100 then 'Elevated'
                        else 'High'
                    end as risk_level
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                left join distinct_sponsor s on s.loan_alias_name = v.loan_alias_name
                left join distinct_interest_status i on i.loan_alias_name = v.loan_alias_name
                left join distinct_exit e on e.loan_alias_name = v.loan_alias_name
                where 1 = 1
                {joinStatusPredicate}
                group by
                    v.loan_alias_name,
                    s.sponsor,
                    i.interest_status_alias,
                    e.ext
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<LoanAliasSummaryRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ltv = GetNullableDecimal(reader, "ltv");
                rows.Add(new LoanAliasSummaryRowDto
                {
                    LoanAliasKey = 0,
                    LoanAlias = GetString(reader, "loan_alias_name"),
                    Sponsor = GetNullableString(reader, "sponsor"),
                    DefaultDate = GetNullableDateTime(reader, "default_date"),
                    MaturityDate = GetNullableDateTime(reader, "maturity_date"),
                    InterestStatus = GetNullableString(reader, "interest_status_alias"),
                    Units = GetNullableString(reader, "units"),
                    Exit = GetNullableString(reader, "ext"),
                    Security = GetNullableDecimal(reader, "security"),
                    Principal = GetNullableDecimal(reader, "principal") ?? 0m,
                    OsInt = GetNullableDecimal(reader, "outstanding_interest") ?? 0m,
                    Accrued = GetNullableDecimal(reader, "accrued") ?? 0m,
                    LateInt = GetNullableDecimal(reader, "late_interest") ?? 0m,
                    TaxIns = GetNullableDecimal(reader, "tax_arrear") ?? 0m,
                    IntAdv = GetNullableDecimal(reader, "interest_adjustment") ?? 0m,
                    Other = GetNullableDecimal(reader, "other_cost") ?? 0m,
                    TotalExposure = GetNullableDecimal(reader, "total_exposure") ?? 0m,
                    Ltv = ltv.HasValue ? Math.Round(ltv.Value, 2) : null,
                    Risk = NormalizeRiskLevel(GetNullableString(reader, "risk_level"))
                });
            }

            await AssignLoanAliasKeysAsync(rows, cancellationToken);
            return rows
                .OrderBy(row => row.LoanAlias, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task AssignLoanAliasKeysAsync(
            List<LoanAliasSummaryRowDto> rows,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var keysByName = await LoadLoanAliasKeyLookupAsync(cancellationToken);
            for (var i = 0; i < rows.Count; i++)
            {
                if (!keysByName.TryGetValue(rows[i].LoanAlias, out var aliasKey))
                {
                    continue;
                }

                var current = rows[i];
                rows[i] = new LoanAliasSummaryRowDto
                {
                    LoanAliasKey = aliasKey,
                    LoanAlias = current.LoanAlias,
                    Sponsor = current.Sponsor,
                    DefaultDate = current.DefaultDate,
                    MaturityDate = current.MaturityDate,
                    InterestStatus = current.InterestStatus,
                    Units = current.Units,
                    Exit = current.Exit,
                    Security = current.Security,
                    Principal = current.Principal,
                    OsInt = current.OsInt,
                    Accrued = current.Accrued,
                    LateInt = current.LateInt,
                    TaxIns = current.TaxIns,
                    IntAdv = current.IntAdv,
                    Other = current.Other,
                    TotalExposure = current.TotalExposure,
                    Ltv = current.Ltv,
                    Risk = current.Risk
                };
            }
        }

        private async Task<Dictionary<string, int>> LoadLoanAliasKeyLookupAsync(CancellationToken cancellationToken)
        {
            if (_loanAliasKeyLookup is not null)
            {
                return new Dictionary<string, int>(_loanAliasKeyLookup, StringComparer.OrdinalIgnoreCase);
            }

            await _loanAliasKeyLookupLock.WaitAsync(cancellationToken);
            try
            {
                if (_loanAliasKeyLookup is not null)
                {
                    return new Dictionary<string, int>(_loanAliasKeyLookup, StringComparer.OrdinalIgnoreCase);
                }

                var keysByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var sql = $"""
                    select loan_alias_id, loan_alias_name
                    from {_tblSubjectiveLoanAliasMaster}
                    where loan_alias_name is not null
                    """;

                try
                {
                    await using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = new SqlCommand(sql, connection);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var name = GetString(reader, "loan_alias_name");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        keysByName[name] = GetInt32(reader, "loan_alias_id");
                    }

                    _loanAliasKeyLookup = keysByName;
                }
                catch (SqlException ex)
                {
                    _logger.LogWarning(ex, "Could not resolve loan_alias_id values for management summary rows.");
                }

                return keysByName;
            }
            finally
            {
                _loanAliasKeyLookupLock.Release();
            }
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadDashboardInvestorSummaryAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            // Single fn_exposure evaluation; share % computed in C#.
            var sql = AppendFundingStatusFilter(
                $"""
                select
                    investor = isnull(nullif(ltrim(rtrim(v.investor_name)), ''), '(Unknown)'),
                    loan_count = count(distinct v.loan_code),
                    exposure = sum(a.exposure)
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                """,
                fundingStatus,
                "v.funding_status_description");

            sql += """

                group by isnull(nullif(ltrim(rtrim(v.investor_name)), ''), '(Unknown)')
                order by exposure desc
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<ChartSliceDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ChartSliceDto
                {
                    Label = GetString(reader, "investor"),
                    Value = GetNullableDecimal(reader, "exposure") ?? 0m,
                    Count = GetInt32(reader, "loan_count")
                });
            }

            var total = rows.Sum(row => row.Value);
            return rows
                .Select(row => new ChartSliceDto
                {
                    Label = row.Label,
                    Value = row.Value,
                    Count = row.Count,
                    SharePercent = total > 0 ? Math.Round(row.Value / total * 100m, 1) : null
                })
                .ToList();
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadDashboardSponsorSummaryAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = AppendFundingStatusFilter(
                $"""
                select
                    sponsor = isnull(nullif(ltrim(rtrim(v.sponsor)), ''), '(Unknown)'),
                    loan_count = count(distinct v.loan_code),
                    exposure = sum(a.exposure),
                    average_ltv = avg(v.ltv)
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                """,
                fundingStatus,
                "v.funding_status_description");

            sql += """

                group by isnull(nullif(ltrim(rtrim(v.sponsor)), ''), '(Unknown)')
                order by average_ltv desc
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<(ChartSliceDto Slice, decimal Exposure)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var exposure = GetNullableDecimal(reader, "exposure") ?? 0m;
                var averageLtv = GetNullableDecimal(reader, "average_ltv");
                rows.Add((
                    new ChartSliceDto
                    {
                        Label = GetString(reader, "sponsor"),
                        Value = exposure,
                        Count = GetInt32(reader, "loan_count"),
                        AverageLtv = averageLtv.HasValue ? Math.Round(averageLtv.Value, 2) : null
                    },
                    exposure));
            }

            var total = rows.Sum(row => row.Exposure);
            return rows
                .Select(row => new ChartSliceDto
                {
                    Label = row.Slice.Label,
                    Value = row.Slice.Value,
                    Count = row.Slice.Count,
                    AverageLtv = row.Slice.AverageLtv,
                    SharePercent = total > 0 ? Math.Round(row.Exposure / total * 100m, 1) : null
                })
                .ToList();
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadDashboardLtvRiskDistributionAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "where v.funding_status_description = @funding_status";

            var sql = $"""
                with temp as (
                    select
                        v.loan_alias_name,
                        case
                            when avg(v.ltv) < 50 then 'Low'
                            when avg(v.ltv) between 50 and 75 then 'Moderate'
                            when avg(v.ltv) between 75 and 100 then 'Elevated'
                            else 'High'
                        end as risk_level,
                        sum(a.exposure) as exposure
                    from {_vwLoanAttributes} v
                    inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                    {statusPredicate}
                    group by v.loan_alias_name
                )
                select
                    risk_level,
                    count(loan_alias_name) as loan_count,
                    sum(exposure) as total_exposure
                from temp
                group by risk_level
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<ChartSliceDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ChartSliceDto
                {
                    Label = NormalizeRiskLevel(GetNullableString(reader, "risk_level")),
                    Value = GetNullableDecimal(reader, "total_exposure") ?? 0m,
                    Count = GetInt32(reader, "loan_count")
                });
            }

            var total = rows.Sum(row => row.Value);
            return rows
                .Select(row => new ChartSliceDto
                {
                    Label = row.Label,
                    Value = row.Value,
                    Count = row.Count,
                    SharePercent = total > 0 ? Math.Round(row.Value / total * 100m, 1) : null
                })
                .OrderByDescending(row => row.Value)
                .ToList();
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadDashboardTop5ExposuresAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "where v.funding_status_description = @funding_status";

            var sql = $"""
                with temp as (
                    select
                        v.loan_alias_name,
                        sum(a.exposure) as exposure
                    from {_vwLoanAttributes} v
                    inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                    {statusPredicate}
                    group by v.loan_alias_name
                ),
                ranked as (
                    select
                        loan_alias_name,
                        exposure,
                        row_number() over (order by exposure desc) as rn,
                        sum(exposure) over () as tot_exposure
                    from temp
                )
                select loan_alias_name, exposure, tot_exposure
                from ranked
                where rn <= 5
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<ChartSliceDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var exposure = GetNullableDecimal(reader, "exposure") ?? 0m;
                var total = GetNullableDecimal(reader, "tot_exposure") ?? 0m;
                rows.Add(new ChartSliceDto
                {
                    Label = GetString(reader, "loan_alias_name"),
                    Value = exposure,
                    SharePercent = total > 0 ? Math.Round(exposure / total * 100m, 1) : null
                });
            }

            return rows;
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadDashboardExposureBreakdownAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = AppendFundingStatusFilter(
                $"""
                select
                    sum(a.exposure) as exposure,
                    sum(a.outstanding_loan_interest) as outstanding_interest,
                    sum(a.accrued) as accrued,
                    sum(a.outstanding_late_interest) as late_interest,
                    sum(isnull(a.tax_arrears, 0)) as tax_arrear,
                    sum(a.monthly_interest_adjustment_amount) as interest_adjustment,
                    sum(isnull(a.outstanding_invoices, 0) + isnull(a.est_realization_costs, 0) + isnull(a.cost_to_complete, 0)) as other_cost
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                """,
                fundingStatus,
                "v.funding_status_description");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return [];
            }

            var totalExposure = GetNullableDecimal(reader, "exposure") ?? 0m;
            var components = new (string Label, decimal Value)[]
            {
                ("Outstanding Interest", GetNullableDecimal(reader, "outstanding_interest") ?? 0m),
                ("Accrued", GetNullableDecimal(reader, "accrued") ?? 0m),
                ("Late Interest", GetNullableDecimal(reader, "late_interest") ?? 0m),
                ("Tax Arrears", GetNullableDecimal(reader, "tax_arrear") ?? 0m),
                ("Interest Adjustment", GetNullableDecimal(reader, "interest_adjustment") ?? 0m),
                ("Other", GetNullableDecimal(reader, "other_cost") ?? 0m)
            };

            var denominator = totalExposure > 0
                ? totalExposure
                : components.Sum(component => component.Value);

            return components
                .Where(component => component.Value != 0m)
                .Select(component => new ChartSliceDto
                {
                    Label = component.Label,
                    Value = component.Value,
                    SharePercent = denominator > 0
                        ? Math.Round(component.Value / denominator * 100m, 1)
                        : null
                })
                .ToList();
        }

        private async Task<List<ExposureAnalysisRowDto>> LoadDashboardExposureAnalysisAsync(
            DateOnly asOfDate,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "where v.funding_status_description = @funding_status";

            var sql = $"""
                with base as (
                    select
                        v.loan_alias_name,
                        v.sponsor,
                        v.loan_code,
                        v.investor_alias_name,
                        isnull(v.ranking, 0) as ranking,
                        a.exposure,
                        v.ltv
                    from {_vwLoanAttributes} v
                    inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                    {statusPredicate}
                ),
                ks_rank as (
                    select
                        loan_alias_name,
                        sponsor,
                        min(ranking) as ks_start_ranking,
                        max(ranking) as ks_end_ranking
                    from base
                    where investor_alias_name in ('SMF', 'MLP')
                    group by loan_alias_name, sponsor
                )
                select
                    b.loan_alias_name,
                    b.sponsor,
                    sum(case when b.ranking < k.ks_start_ranking then b.exposure else 0 end) as external_balance,
                    sum(case when b.investor_alias_name = 'SMF' then b.exposure else 0 end) as smf_balance,
                    sum(case when b.investor_alias_name = 'MLP' then b.exposure else 0 end) as mlp_balance,
                    sum(case when b.ranking > k.ks_end_ranking then b.exposure else 0 end) as subordinate_exposure,
                    sum(case when b.ranking < k.ks_start_ranking then b.exposure else 0 end)
                        + sum(case when b.investor_alias_name in ('SMF', 'MLP') then b.exposure else 0 end) as total_ks_exposure,
                    avg(b.ltv) as ltv
                from base b
                inner join ks_rank k
                    on b.loan_alias_name = k.loan_alias_name
                    and isnull(b.sponsor, '') = isnull(k.sponsor, '')
                group by b.loan_alias_name, b.sponsor
                order by b.loan_alias_name, b.sponsor
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddAsOfDateParameter(command, asOfDate);
            AddFundingStatusParameter(command, fundingStatus);

            var rows = new List<ExposureAnalysisRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ltv = GetNullableDecimal(reader, "ltv");
                rows.Add(new ExposureAnalysisRowDto
                {
                    LoanAliasKey = 0,
                    LoanAlias = GetString(reader, "loan_alias_name"),
                    Sponsor = GetNullableString(reader, "sponsor") ?? string.Empty,
                    ExternalBalance = GetNullableDecimal(reader, "external_balance") ?? 0m,
                    SmfBalance = GetNullableDecimal(reader, "smf_balance") ?? 0m,
                    MlpBalance = GetNullableDecimal(reader, "mlp_balance") ?? 0m,
                    TotalKsExposure = GetNullableDecimal(reader, "total_ks_exposure") ?? 0m,
                    SubordinateExposure = GetNullableDecimal(reader, "subordinate_exposure") ?? 0m,
                    Ltv = ltv.HasValue ? Math.Round(ltv.Value, 2) : null
                });
            }

            var keysByName = await LoadLoanAliasKeyLookupAsync(cancellationToken);
            for (var i = 0; i < rows.Count; i++)
            {
                if (!keysByName.TryGetValue(rows[i].LoanAlias, out var aliasKey))
                {
                    continue;
                }

                var current = rows[i];
                rows[i] = new ExposureAnalysisRowDto
                {
                    LoanAliasKey = aliasKey,
                    LoanAlias = current.LoanAlias,
                    Sponsor = current.Sponsor,
                    ExternalBalance = current.ExternalBalance,
                    SmfBalance = current.SmfBalance,
                    MlpBalance = current.MlpBalance,
                    TotalKsExposure = current.TotalKsExposure,
                    SubordinateExposure = current.SubordinateExposure,
                    Ltv = current.Ltv
                };
            }

            return rows;
        }

        private static List<ExposureAnalysisRowDto> ApplyExposureAnalysisFilters(
            List<ExposureAnalysisRowDto> rows,
            ManagementSummaryDashboardQuery query)
        {
            IEnumerable<ExposureAnalysisRowDto> filtered = rows;

            if (!string.IsNullOrWhiteSpace(query.Sponsor)
                && !query.Sponsor.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row =>
                    row.Sponsor.Contains(query.Sponsor, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        private async Task<ManagementSummaryFilterOptionsDto> LoadMortgageViewFilterOptionsAsync(
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var cacheKey = fundingStatus ?? string.Empty;
            if (_filterOptionsCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var sql = $"""
                select distinct
                    sponsor = ltrim(rtrim(sponsor)),
                    investor_name = ltrim(rtrim(investor_name))
                from {_vwLoanAttributes}
                """;
            if (!string.IsNullOrEmpty(fundingStatus))
            {
                sql += """

                    where funding_status_description = @funding_status
                    """;
            }

            var sponsors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "All" };
            var investors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "All" };

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            if (!string.IsNullOrEmpty(fundingStatus))
            {
                command.Parameters.AddWithValue("@funding_status", fundingStatus);
            }

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sponsor = GetNullableString(reader, "sponsor");
                    if (!string.IsNullOrWhiteSpace(sponsor))
                    {
                        sponsors.Add(sponsor);
                    }

                    var investor = GetNullableString(reader, "investor_name");
                    if (!string.IsNullOrWhiteSpace(investor))
                    {
                        investors.Add(investor);
                    }
                }
            }

            var statuses = await LoadDimStatusFilterLabelsAsync(connection, cancellationToken);

            var options = new ManagementSummaryFilterOptionsDto
            {
                Sponsors = sponsors.ToList(),
                InvestorAliases = investors.ToList(),
                Statuses = statuses
            };
            _filterOptionsCache[cacheKey] = options;
            return options;
        }

        private async Task<IReadOnlyList<string>> LoadDimStatusFilterLabelsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var statuses = new List<string>();
            var statusSql = $"""
                select s.status_name
                from {_tblDimStatus} s
                where isnull(s.is_active, 1) = 1
                  and isnull(s.status_type, 'FUNDING') = 'FUNDING'
                  and s.status_name is not null
                  and ltrim(rtrim(s.status_name)) <> ''
                order by isnull(s.sort_order, 999999), s.status_name
                """;

            try
            {
                await using var statusCmd = new SqlCommand(statusSql, connection);
                await using var statusReader = await statusCmd.ExecuteReaderAsync(cancellationToken);
                while (await statusReader.ReadAsync(cancellationToken))
                {
                    statuses.Add(statusReader.GetString(0).Trim());
                }
            }
            catch (SqlException ex)
            {
                _logger.LogDebug(ex, "Status filter options skipped; shared.dim_status unavailable.");
                statuses.AddRange(["Unfunded", "Funded", "Default", "Repaid"]);
            }

            statuses.Add("All");
            return statuses;
        }

        private static void AddAsOfDateParameter(SqlCommand command, DateOnly asOfDate) =>
            command.Parameters.AddWithValue("@as_of_date", asOfDate.ToString("yyyy-MM-dd"));

        private static IReadOnlyList<CmhcWatchlistRowDto> DeduplicateWatchlistRows(
            IReadOnlyList<CmhcWatchlistRowDto> rows) =>
            rows
                .GroupBy(
                    row => $"{row.LoanId}|{row.Investor}|{row.Property}|{row.ReportDate:yyyy-MM-dd}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

        private static string NormalizeRiskLevel(string? risk) =>
            string.IsNullOrWhiteSpace(risk) ? "LOW" : risk.Trim().ToUpperInvariant();

        public async Task<LoanDetailReportDashboardDto> GetLoanDetailReportAsync(
            int loanAliasKey,
            LoanDetailReportQuery query,
            CancellationToken cancellationToken = default)
        {
            var fundingStatus = ResolveFundingStatusDescription(query.Statuses);
            var aliasName = await ResolveLoanAliasNameAsync(loanAliasKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(aliasName))
            {
                _logger.LogWarning(
                    "Could not resolve loan_alias_name for loan detail report alias key {LoanAliasKey}.",
                    loanAliasKey);
                return new LoanDetailReportDashboardDto
                {
                    LoanAlias = string.Empty,
                    KeyDates = new LoanDetailReportKeyDatesDto { AsOfDate = query.AsOfDate }
                };
            }

            var portfolioTask = LoadLoanDetailPortfolioRowsAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var topBarTask = LoadLoanDetailTopBarAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var reportDetailsTask = LoadLoanDetailReportDetailsAsync(
                aliasName, fundingStatus, cancellationToken);
            var keyDatesTask = LoadLoanDetailKeyDatesAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var propertyStatsTask = LoadLoanDetailPropertyStatsAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var interestReserveTask = LoadLoanDetailInterestReserveAsync(
                aliasName, fundingStatus, cancellationToken);
            var exposureByInvestorTask = LoadLoanDetailExposureByInvestorAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var exposureCompositionTask = LoadLoanDetailExposureCompositionAsync(
                query.AsOfDate, aliasName, fundingStatus, cancellationToken);
            var taxTask = LoadTaxArrearsForAliasAsync(loanAliasKey, cancellationToken);

            await Task.WhenAll(
                portfolioTask,
                topBarTask,
                reportDetailsTask,
                keyDatesTask,
                propertyStatsTask,
                interestReserveTask,
                exposureByInvestorTask,
                exposureCompositionTask,
                taxTask);

            var portfolioRows = await portfolioTask;
            var topBar = await topBarTask;
            var reportDetails = await reportDetailsTask;
            var keyDatesRaw = await keyDatesTask;
            var propertyStats = await propertyStatsTask;
            var interestReserve = await interestReserveTask;
            var exposureByInvestor = await exposureByInvestorTask;
            var exposureComposition = await exposureCompositionTask;
            var taxData = await taxTask;

            var portfolioTotals = BuildPortfolioTotals(portfolioRows);
            var totalExposure = portfolioTotals.TotalExposure;
            var securityValue = topBar.SecurityValue ?? propertyStats.SecurityValue;
            var overallLtv = topBar.AverageLtv;
            var equityCushion = securityValue.HasValue ? securityValue - totalExposure : null;

            DateTime? dateOfDefault = keyDatesRaw.DefaultDate;
            int? daysInDefault = dateOfDefault.HasValue
                ? (int)(query.AsOfDate.ToDateTime(TimeOnly.MinValue) - dateOfDefault.Value.Date).TotalDays
                : null;

            decimal? monthsCovered = null;
            var reserveBalance = interestReserve.CurrentInterestReserveBalance ?? 0m;
            var monthlyInterest = topBar.TotalOutstandingInterest / 12m;
            if (reserveBalance > 0 && monthlyInterest > 0)
            {
                monthsCovered = Math.Round(reserveBalance / monthlyInterest, 1);
            }

            var unitsLabel = propertyStats.Units?.ToString("G29");
            int? ranking = portfolioRows.Any(row => row.Rank.HasValue)
                ? portfolioRows.Where(row => row.Rank.HasValue).Min(row => row.Rank)
                : null;

            _logger.LogInformation(
                "Loan detail report for alias {LoanAliasKey} ({AliasName}): {PortfolioCount} portfolio rows.",
                loanAliasKey,
                aliasName,
                portfolioRows.Count);

            return new LoanDetailReportDashboardDto
            {
                LoanAlias = aliasName,
                Header = new LoanDetailReportHeaderDto
                {
                    SecurityValue = securityValue,
                    OverallLtv = overallLtv,
                    EquityCushion = equityCushion,
                    Units = unitsLabel
                },
                ReportDetails = new LoanDetailReportDetailsDto
                {
                    MainLoanId = reportDetails.ParentLoanCodes,
                    LoanType = null,
                    InvestorAlias = portfolioRows
                        .Select(row => row.Investor)
                        .FirstOrDefault(investor => !string.IsNullOrWhiteSpace(investor)),
                    Ranking = ranking
                },
                KeyDates = new LoanDetailReportKeyDatesDto
                {
                    DateOfDefault = dateOfDefault,
                    DaysInDefault = daysInDefault > 0 ? daysInDefault : null,
                    MaturityDate = keyDatesRaw.MaturityDate,
                    AsOfDate = query.AsOfDate
                },
                PropertyStats = new LoanDetailReportPropertyStatsDto
                {
                    ValuePerUnit = propertyStats.ValuePerUnit,
                    RiskStatus = MapDashboardRiskBand(overallLtv),
                    PropertyType = null,
                    Location = reportDetails.Sponsors
                },
                InterestSummary = new LoanDetailReportInterestSummaryDto
                {
                    InterestDisbursed = topBar.InterestDisbursed,
                    InterestNotDisbursed = topBar.InterestNotDisbursed,
                    TotalOutstandingInterest = topBar.TotalOutstandingInterest,
                    MonthsInArrears = null
                },
                InterestReserve = new LoanDetailReportInterestReserveDto
                {
                    CurrentInterestReserve = interestReserve.CurrentInterestReserve,
                    CurrentInterestReserveBalance = interestReserve.CurrentInterestReserveBalance,
                    MonthsCoveredByReserve = monthsCovered
                },
                PortfolioRows = portfolioRows,
                PortfolioTotals = portfolioTotals,
                ExposureByInvestor = exposureByInvestor,
                ExposureComposition = exposureComposition,
                InvestorBreakdown = exposureByInvestor,
                TaxArrearsAsAt = taxData.AsAt,
                TaxArrearsByYear = taxData.ByYear
            };
        }

        private async Task<string?> ResolveLoanAliasNameAsync(
            int loanAliasKey,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select top 1 loan_alias_name
                from {_tblSubjectiveLoanAliasMaster}
                where loan_alias_id = @loan_alias_id
                """;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@loan_alias_id", loanAliasKey);
                var result = await command.ExecuteScalarAsync(cancellationToken);
                var name = Convert.ToString(result);
                return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Failed resolving loan_alias_name for key {LoanAliasKey}.", loanAliasKey);
                return null;
            }
        }

        private static string BuildLoanAliasWhere(string? fundingStatus, string aliasColumn = "v.loan_alias_name")
        {
            var sql = $"where {aliasColumn} = @loan_alias_name";
            if (!string.IsNullOrEmpty(fundingStatus))
            {
                sql += " and v.funding_status_description = @funding_status";
            }

            return sql;
        }

        private static void AddLoanAliasParameters(
            SqlCommand command,
            DateOnly? asOfDate,
            string loanAliasName,
            string? fundingStatus)
        {
            if (asOfDate.HasValue)
            {
                AddAsOfDateParameter(command, asOfDate.Value);
            }

            command.Parameters.AddWithValue("@loan_alias_name", loanAliasName);
            AddFundingStatusParameter(command, fundingStatus);
        }

        private async Task<List<LoanPortfolioDetailRowDto>> LoadLoanDetailPortfolioRowsAsync(
            DateOnly asOfDate,
            string loanAliasName,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    v.loan_code,
                    v.loan_description,
                    v.investor_name,
                    v.ranking,
                    cast(null as decimal(18, 6)) as rate,
                    a.principal_balance,
                    a.outstanding_loan_interest,
                    a.accrued,
                    a.outstanding_late_interest,
                    a.monthly_interest_adjustment_amount,
                    a.tax_arrears,
                    (isnull(a.outstanding_invoices, 0) + isnull(a.est_realization_costs, 0) + isnull(a.cost_to_complete, 0)) as other_cost,
                    a.exposure,
                    v.ltv
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                order by v.ranking, v.loan_code
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            var rows = new List<LoanPortfolioDetailRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ltv = GetNullableDecimal(reader, "ltv");
                rows.Add(new LoanPortfolioDetailRowDto
                {
                    LoanId = GetString(reader, "loan_code"),
                    Description = GetNullableString(reader, "loan_description") ?? string.Empty,
                    Investor = GetNullableString(reader, "investor_name") ?? string.Empty,
                    Rank = GetNullableInt32(reader, "ranking"),
                    Rate = GetNullableDecimal(reader, "rate"),
                    Principal = GetNullableDecimal(reader, "principal_balance") ?? 0m,
                    DefInterest = GetNullableDecimal(reader, "outstanding_loan_interest") ?? 0m,
                    AccruedInt = GetNullableDecimal(reader, "accrued") ?? 0m,
                    LateInt = GetNullableDecimal(reader, "outstanding_late_interest") ?? 0m,
                    IntAdj = GetNullableDecimal(reader, "monthly_interest_adjustment_amount") ?? 0m,
                    TaxArrears = GetNullableDecimal(reader, "tax_arrears") ?? 0m,
                    OtherCosts = GetNullableDecimal(reader, "other_cost") ?? 0m,
                    TotalExposure = GetNullableDecimal(reader, "exposure") ?? 0m,
                    Ltv = ltv.HasValue ? Math.Round(ltv.Value, 2) : null,
                    MonthsInArrears = null,
                    TimesNsfd = null
                });
            }

            return rows;
        }

        private async Task<(
            decimal? SecurityValue,
            decimal? AverageLtv,
            decimal InterestDisbursed,
            decimal InterestNotDisbursed,
            decimal TotalOutstandingInterest)> LoadLoanDetailTopBarAsync(
            DateOnly asOfDate,
            string loanAliasName,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    max(v.security_value_loan_alias) as security_value,
                    avg(v.ltv) as ltv,
                    sum(a.interest_disbursed) as interest_disbursed,
                    sum(a.interest_not_disbursed) as interest_not_disbursed,
                    sum(a.outstanding_loan_interest) as total_outstanding_interest
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                group by v.loan_alias_name
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (null, null, 0m, 0m, 0m);
            }

            var averageLtv = GetNullableDecimal(reader, "ltv");
            return (
                GetNullableDecimal(reader, "security_value"),
                averageLtv.HasValue ? Math.Round(averageLtv.Value, 2) : null,
                GetNullableDecimal(reader, "interest_disbursed") ?? 0m,
                GetNullableDecimal(reader, "interest_not_disbursed") ?? 0m,
                GetNullableDecimal(reader, "total_outstanding_interest") ?? 0m);
        }

        private async Task<(string? ParentLoanCodes, string? Sponsors, int InvestorCount)>
            LoadLoanDetailReportDetailsAsync(
                string loanAliasName,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "and funding_status_description = @funding_status";

            var sql = $"""
                select
                    string_agg(parent_loan_code, ', ') as parent_loan_codes,
                    string_agg(sponsor, ', ') as sponsors,
                    count(distinct investor_code) as investor_count
                from (
                    select distinct
                        loan_alias_name,
                        parent_loan_code,
                        sponsor,
                        investor_code
                    from {_vwLoanAttributes}
                    where loan_alias_name = @loan_alias_name
                    {statusPredicate}
                ) x
                group by loan_alias_name
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, null, loanAliasName, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (null, null, 0);
            }

            return (
                GetNullableString(reader, "parent_loan_codes"),
                GetNullableString(reader, "sponsors"),
                GetInt32(reader, "investor_count"));
        }

        private async Task<(DateTime? DefaultDate, DateTime? MaturityDate, DateTime? DateOfAdvance, DateTime? InterestOffDate)>
            LoadLoanDetailKeyDatesAsync(
                DateOnly asOfDate,
                string loanAliasName,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    min(a.date_of_advance) as date_of_advance,
                    min(case when v.default_date is null then v.loan_term_default_date else v.default_date end) as default_date,
                    min(v.maturity_date) as maturity_date,
                    min(v.date_interest_turned_off) as interest_off_date
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                group by v.loan_alias_name
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (null, null, null, null);
            }

            return (
                GetNullableDateTime(reader, "default_date"),
                GetNullableDateTime(reader, "maturity_date"),
                GetNullableDateTime(reader, "date_of_advance"),
                GetNullableDateTime(reader, "interest_off_date"));
        }

        private async Task<(decimal? SecurityValue, decimal? Units, decimal? ValuePerUnit, decimal? ExposurePerUnit)>
            LoadLoanDetailPropertyStatsAsync(
                DateOnly asOfDate,
                string loanAliasName,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    max(v.security_value_loan_alias) as security_value,
                    max(v.units_loan_alias) as units,
                    case
                        when nullif(max(v.units_loan_alias), 0) is null then null
                        else max(v.security_value_loan_alias) / nullif(max(v.units_loan_alias), 0)
                    end as value_per_unit,
                    case
                        when nullif(max(v.units_loan_alias), 0) is null then null
                        else sum(a.exposure) / nullif(max(v.units_loan_alias), 0)
                    end as exposure_per_unit
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                group by v.loan_alias_name
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (null, null, null, null);
            }

            var valuePerUnit = GetNullableDecimal(reader, "value_per_unit");
            var exposurePerUnit = GetNullableDecimal(reader, "exposure_per_unit");
            return (
                GetNullableDecimal(reader, "security_value"),
                GetNullableDecimal(reader, "units"),
                valuePerUnit.HasValue ? Math.Round(valuePerUnit.Value, 2) : null,
                exposurePerUnit.HasValue ? Math.Round(exposurePerUnit.Value, 2) : null);
        }

        private async Task<(decimal? CurrentInterestReserve, decimal? CurrentInterestReserveBalance)>
            LoadLoanDetailInterestReserveAsync(
                string loanAliasName,
                string? fundingStatus,
                CancellationToken cancellationToken)
        {
            var statusPredicate = string.IsNullOrEmpty(fundingStatus)
                ? string.Empty
                : "and v.funding_status_description = @funding_status";

            var sql = $"""
                with interest_reserve as (
                    select
                        loan_key,
                        loan_code,
                        sum(case
                            when trim(a.history_status) = 'Interest Reserve Funding'
                                then a.interest_reserve_draw_amount
                        end) as current_interest_reserve,
                        sum(case
                            when trim(a.history_status) <> 'Interest Reserve Funding'
                                then a.interest_reserve_draw_amount
                        end) as current_interest_reserve_balance
                    from {_tblFactAmortizationSchedule} a
                    group by loan_key, loan_code
                )
                select
                    sum(i.current_interest_reserve) as current_interest_reserve,
                    sum(i.current_interest_reserve_balance) as current_interest_reserve_balance
                from {_vwLoanAttributes} v
                left join interest_reserve i on v.loan_code = i.loan_code
                where v.loan_alias_name = @loan_alias_name
                {statusPredicate}
                group by v.loan_alias_name
                """;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection);
                AddLoanAliasParameters(command, null, loanAliasName, fundingStatus);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return (null, null);
                }

                return (
                    GetNullableDecimal(reader, "current_interest_reserve"),
                    GetNullableDecimal(reader, "current_interest_reserve_balance"));
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Interest reserve query failed for alias {LoanAliasName}; returning empty reserve.",
                    loanAliasName);
                return (null, null);
            }
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadLoanDetailExposureByInvestorAsync(
            DateOnly asOfDate,
            string loanAliasName,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    investor = isnull(nullif(ltrim(rtrim(v.investor_name)), ''), '(Unknown)'),
                    exposure = sum(a.exposure)
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                group by isnull(nullif(ltrim(rtrim(v.investor_name)), ''), '(Unknown)')
                order by exposure desc
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            var rows = new List<ChartSliceDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ChartSliceDto
                {
                    Label = GetString(reader, "investor"),
                    Value = GetNullableDecimal(reader, "exposure") ?? 0m
                });
            }

            var total = rows.Sum(row => row.Value);
            return rows
                .Where(row => row.Value != 0m)
                .Select(row => new ChartSliceDto
                {
                    Label = row.Label,
                    Value = row.Value,
                    SharePercent = total > 0 ? Math.Round(row.Value / total * 100m, 1) : null
                })
                .ToList();
        }

        private async Task<IReadOnlyList<ChartSliceDto>> LoadLoanDetailExposureCompositionAsync(
            DateOnly asOfDate,
            string loanAliasName,
            string? fundingStatus,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select
                    sum(a.exposure) as exposure,
                    sum(a.outstanding_loan_interest) as outstanding_interest,
                    sum(a.accrued) as accrued,
                    sum(a.outstanding_late_interest) as late_interest,
                    sum(isnull(a.tax_arrears, 0)) as tax_arrear,
                    sum(a.monthly_interest_adjustment_amount) as interest_adjustment,
                    sum(isnull(a.outstanding_invoices, 0) + isnull(a.est_realization_costs, 0) + isnull(a.cost_to_complete, 0)) as other_cost
                from {_vwLoanAttributes} v
                inner join {_fnExposure}(@as_of_date) a on a.loan_code = v.loan_code
                {BuildLoanAliasWhere(fundingStatus)}
                group by v.loan_alias_name
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, asOfDate, loanAliasName, fundingStatus);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return [];
            }

            var totalExposure = GetNullableDecimal(reader, "exposure") ?? 0m;
            var components = new (string Label, decimal Value)[]
            {
                ("Outstanding Interest", GetNullableDecimal(reader, "outstanding_interest") ?? 0m),
                ("Accrued", GetNullableDecimal(reader, "accrued") ?? 0m),
                ("Late Interest", GetNullableDecimal(reader, "late_interest") ?? 0m),
                ("Tax Arrears", GetNullableDecimal(reader, "tax_arrear") ?? 0m),
                ("Interest Adjustment", GetNullableDecimal(reader, "interest_adjustment") ?? 0m),
                ("Other", GetNullableDecimal(reader, "other_cost") ?? 0m)
            };

            var denominator = totalExposure > 0
                ? totalExposure
                : components.Sum(component => component.Value);

            return components
                .Where(component => component.Value != 0m)
                .Select(component => new ChartSliceDto
                {
                    Label = component.Label,
                    Value = component.Value,
                    SharePercent = denominator > 0
                        ? Math.Round(component.Value / denominator * 100m, 1)
                        : null
                })
                .ToList();
        }

        private static string MapDashboardRiskBand(decimal? ltv)
        {
            if (!ltv.HasValue)
            {
                return "LOW";
            }

            return ltv.Value switch
            {
                < 50m => "LOW",
                <= 75m => "MODERATE",
                <= 100m => "ELEVATED",
                _ => "HIGH"
            };
        }

        private async Task<DashboardColumnMap> ResolveDashboardColumnsAsync(CancellationToken cancellationToken)
        {
            if (_dashboardColumns is not null)
            {
                return _dashboardColumns;
            }

            var principal = await FindColumnAsync(PrincipalColumnCandidates, cancellationToken);
            if (string.IsNullOrEmpty(principal))
            {
                _logger.LogWarning(
                    "mort.dim_loan has no principal/exposure column from [{Columns}]; amounts default to 0.",
                    string.Join(", ", PrincipalColumnCandidates));
            }

            var map = new DashboardColumnMap
            {
                Principal = principal,
                AccrualPostedDate = await FindColumnAsync(AccrualPostedDateColumnCandidates, cancellationToken),
                FundingStatusKey = await FindColumnAsync(FundingStatusKeyColumnCandidates, cancellationToken),
                InterestRate = await FindColumnAsync(InterestRateColumnCandidates, cancellationToken),
                Sponsor = await FindColumnAsync(SponsorColumnCandidates, cancellationToken),
                PropertyAddress = await FindColumnAsync(PropertyAddressColumnCandidates, cancellationToken),
                PropertyType = await FindColumnAsync(PropertyTypeColumnCandidates, cancellationToken),
                LoanType = await FindColumnAsync(LoanTypeColumnCandidates, cancellationToken),
                OutstandingInterest = await FindColumnAsync(OutstandingInterestColumnCandidates, cancellationToken),
                AccruedInterest = await FindColumnAsync(AccruedInterestColumnCandidates, cancellationToken),
                LateInterest = await FindColumnAsync(LateInterestColumnCandidates, cancellationToken),
                InterestDisbursed = await FindColumnAsync(InterestDisbursedColumnCandidates, cancellationToken),
                InterestNotDisbursed = await FindColumnAsync(InterestNotDisbursedColumnCandidates, cancellationToken),
                DefaultInterest = await FindColumnAsync(DefaultInterestColumnCandidates, cancellationToken),
                InterestAdjustment = await FindColumnAsync(InterestAdjustmentColumnCandidates, cancellationToken),
                InterestAdvance = await FindColumnAsync(InterestAdvanceColumnCandidates, cancellationToken),
                OutstandingInvoice = await FindColumnAsync(OutstandingInvoiceColumnCandidates, cancellationToken),
                EstimatedRealization = await FindColumnAsync(EstimatedRealizationColumnCandidates, cancellationToken),
                CostToComplete = await FindColumnAsync(CostToCompleteColumnCandidates, cancellationToken),
                MonthsInArrears = await FindColumnAsync(MonthsInArrearsColumnCandidates, cancellationToken),
                TimesNsfd = await FindColumnAsync(TimesNsfdColumnCandidates, cancellationToken),
                InterestReserve = await FindColumnAsync(InterestReserveColumnCandidates, cancellationToken),
                InterestReserveBalance = await FindColumnAsync(InterestReserveBalanceColumnCandidates, cancellationToken),
                SmfInterestStatus = await FindColumnAsync(SmfInterestStatusColumnCandidates, cancellationToken),
                MlpInterestStatus = await FindColumnAsync(MlpInterestStatusColumnCandidates, cancellationToken),
                MaturityDate = await FindColumnAsync(MaturityDateColumnCandidates, cancellationToken),
                ExitPlan = await FindColumnAsync(ExitPlanColumnCandidates, cancellationToken),
                ExitDate = await FindColumnAsync(ExitDateColumnCandidates, cancellationToken),
                DefaultDate = await GetDefaultDateColumnAsync(cancellationToken),
                Exposure = await GetExposureColumnAsync(cancellationToken),
                DimLoanLtv = await GetDimLoanLtvColumnAsync(cancellationToken),
                ParentLoanKey = await GetParentLoanKeyColumnAsync(cancellationToken),
                LoanStatusKey = await FindColumnAsync(
                    ["loan_status_key", "status_key", "funding_status_key"],
                    cancellationToken),
                WatchlistIssue = await FindColumnAsync(WatchlistIssueColumnCandidates, cancellationToken),
                WatchlistConclusion = await FindColumnAsync(WatchlistConclusionColumnCandidates, cancellationToken),
                WatchlistStatusUpdate = await FindColumnAsync(WatchlistStatusUpdateColumnCandidates, cancellationToken),
                WatchlistDscr = await FindColumnAsync(WatchlistDscrColumnCandidates, cancellationToken),
                WatchlistMissed = await FindColumnAsync(WatchlistMissedColumnCandidates, cancellationToken),
                WatchlistStatus = await FindColumnAsync(WatchlistStatusColumnCandidates, cancellationToken),
                DefaultSubjectiveStatus = await FindColumnAsync(
                    ["default_subjective_status", "default_status_subjective"],
                    cancellationToken)
            };

            _dashboardColumns = map;
            return map;
        }

        private async Task<string?> ResolveLoanStatusKeyColumnAsync(CancellationToken cancellationToken)
        {
            var column = await FindColumnAsync(
                ["loan_status_key", "status_key", "loan_status_id", "status_id", "funding_status_key"],
                cancellationToken);

            return string.IsNullOrEmpty(column) ? null : column;
        }

        private Task<string?> FindColumnAsync(
            IReadOnlyList<string> candidates,
            CancellationToken cancellationToken) =>
            DimLoanColumnProbe.FindFirstAsync(_connectionString, _tblDimLoan, candidates, cancellationToken);

        private async Task<IReadOnlyList<LoanSnapshotRow>> LoadLoanSnapshotRowsAsync(
            DateOnly asOfDate,
            IReadOnlyList<int>? loanAliasIds,
            LoanStatusFilter statusFilter,
            DashboardColumnMap columns,
            CancellationToken cancellationToken)
        {
            await EnsureLtvTableAvailableAsync(cancellationToken);
            var sql = await BuildLoanSnapshotSqlAsync(loanAliasIds, statusFilter, columns, cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@as_of_date", asOfDate.ToDateTime(TimeOnly.MinValue));
            AddLoanAliasParameters(command, loanAliasIds);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<LoanSnapshotRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapLoanSnapshotRow(reader, columns));
            }

            return rows;
        }

        private async Task<string> BuildLoanSnapshotSqlAsync(
            IReadOnlyList<int>? loanAliasIds,
            LoanStatusFilter statusFilter,
            DashboardColumnMap columns,
            CancellationToken cancellationToken)
        {
            var parentLoanKeyColumn = columns.ParentLoanKey;
            var ltvExpr = BuildLtvExpression(columns.DimLoanLtv);
            var resolvedLoanStatusKey = columns.LoanStatusKey
                ?? await ResolveLoanStatusKeyColumnAsync(cancellationToken);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
            }

            var sql = new StringBuilder();
            sql.AppendLine("select l.loan_key,");
            sql.AppendLine("       l.loan_code,");
            sql.AppendLine("       l.loan_desc,");
            sql.AppendLine("       l.loan_alias_key,");
            sql.AppendLine("       loan_alias_name = isnull(m.loan_alias_name, ''),");
            sql.AppendLine("       investor_alias_name = isnull(iam.investor_alias_name, ''),");
            sql.AppendLine("       m.security_value,");
            sql.AppendLine("       m.units,");
            sql.AppendLine("       m.square_feet,");
            sql.AppendLine("       m.acres,");
            sql.AppendLine(string.IsNullOrEmpty(columns.Principal)
                ? "       principal_balance = cast(0 as decimal(18, 2)),"
                : $"       principal_balance = isnull(l.{columns.Principal}, 0),");
            sql.AppendLine(ColExpr(columns.OutstandingInterest, "outstanding_interest"));
            sql.AppendLine(ColExpr(columns.AccruedInterest, "accrued_interest"));
            sql.AppendLine(ColExpr(columns.LateInterest, "late_interest"));
            sql.AppendLine(ColExpr(columns.InterestDisbursed, "interest_disbursed"));
            sql.AppendLine(ColExpr(columns.InterestNotDisbursed, "interest_not_disbursed"));
            sql.AppendLine(ColExpr(columns.DefaultInterest, "default_interest"));
            sql.AppendLine(ColExpr(columns.InterestAdjustment, "interest_adjustment"));
            sql.AppendLine(ColExpr(columns.InterestAdvance, "interest_advance"));
            sql.AppendLine($"       other_costs = {BuildOtherCostsExpression(columns)},");
            sql.AppendLine(ColExpr(columns.MonthsInArrears, "months_in_arrears", "int"));
            sql.AppendLine(ColExpr(columns.TimesNsfd, "times_nsfd", "int"));
            sql.AppendLine(ColExpr(columns.InterestReserve, "interest_reserve"));
            sql.AppendLine(ColExpr(columns.InterestReserveBalance, "interest_reserve_balance"));
            sql.AppendLine(ColExpr(columns.Sponsor, "sponsor", "varchar"));
            sql.AppendLine(ColExpr(columns.PropertyAddress, "property_address", "varchar"));
            sql.AppendLine(ColExpr(columns.PropertyType, "property_type", "varchar"));
            sql.AppendLine(ColExpr(columns.LoanType, "loan_type", "varchar"));
            sql.AppendLine(ColExpr(columns.InterestRate, "interest_rate"));
            sql.AppendLine("       l.loan_ranking as loan_ranking,");
            sql.AppendLine(ColExpr(columns.MaturityDate, "maturity_date", "datetime2"));
            sql.AppendLine(ColExpr(columns.DefaultDate, "default_date", "datetime2"));
            sql.AppendLine(ColExpr(columns.ExitPlan, "exit_plan", "varchar"));
            sql.AppendLine(ColExpr(columns.ExitDate, "exit_date", "varchar"));
            sql.AppendLine(ColExpr(columns.SmfInterestStatus, "smf_interest_status", "varchar"));
            sql.AppendLine(ColExpr(columns.MlpInterestStatus, "mlp_interest_status", "varchar"));
            sql.AppendLine($"       ltv_value = {ltvExpr},");

            if (!string.IsNullOrEmpty(columns.FundingStatusKey))
            {
                sql.AppendLine("       funding_status_name = isnull(fs.status_name, ''),");
            }
            else
            {
                sql.AppendLine("       funding_status_name = cast('' as varchar(100)),");
            }

            if (!string.IsNullOrEmpty(resolvedLoanStatusKey))
            {
                sql.AppendLine("       loan_status_name = isnull(ls.status_name, ''),");
            }
            else
            {
                sql.AppendLine("       loan_status_name = cast('' as varchar(100)),");
            }

            sql.AppendLine(ColExpr(columns.DefaultSubjectiveStatus, "default_subjective_status", "varchar"));

            if (!string.IsNullOrEmpty(parentLoanKeyColumn))
            {
                sql.AppendLine("       parent_loan_id = isnull(parent.loan_code, isnull(l.dummy_loan_link, '')),");
            }
            else
            {
                sql.AppendLine("       parent_loan_id = isnull(l.dummy_loan_link, ''),");
            }

            sql.AppendLine("       tax_arrears_amount = isnull(tax.tax_arrears, 0)");
            sql.Append(await BuildSnapshotFromClauseAsync(
                columns,
                parentLoanKeyColumn,
                resolvedLoanStatusKey,
                cancellationToken));
            sql.Append(LoanEligibleWhere);

            if (!string.IsNullOrEmpty(columns.AccrualPostedDate))
            {
                sql.AppendLine($"  and cast(l.{columns.AccrualPostedDate} as date) = cast(@as_of_date as date)");
            }

            AppendLoanAliasFilter(sql, loanAliasIds);

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine(" order by m.loan_alias_name, l.loan_code");
            return sql.ToString();
        }

        private async Task<string> BuildSnapshotFromClauseAsync(
            DashboardColumnMap columns,
            string? parentLoanKeyColumn,
            string? loanStatusKeyColumn,
            CancellationToken cancellationToken)
        {
            var taxJoin = await BuildTaxArrearsJoinAsync(cancellationToken);
            var fundingJoin = !string.IsNullOrEmpty(columns.FundingStatusKey)
                ? $"""

                  left join {_tblDimStatus} fs
                      on l.{columns.FundingStatusKey} = fs.status_key

                  """
                : string.Empty;

            var loanStatusJoin = !string.IsNullOrEmpty(loanStatusKeyColumn)
                ? $"""

                  left join {_tblDimStatus} ls
                      on l.{loanStatusKeyColumn} = ls.status_key

                  """
                : string.Empty;

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
                    {fundingJoin}
                    {loanStatusJoin}
                    {taxJoin}

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
                {fundingJoin}
                {loanStatusJoin}
                {taxJoin}

                """;
        }

        private async Task<string> BuildTaxArrearsJoinAsync(CancellationToken cancellationToken)
        {
            await EnsureTaxArrearsTableAvailableAsync(cancellationToken);
            if (_taxArrearsTableAvailable != true)
            {
                return """

                    left join (
                        select cast(null as bigint) as loan_key, cast(0 as decimal(18, 2)) as tax_arrears
                        where 1 = 0
                    ) tax on l.loan_key = tax.loan_key

                    """;
            }

            return $"""

                left join (
                    select ta.loan_key,
                           sum(ta.tax_arrears) as tax_arrears
                    from {_tblTaxArrears} ta
                    inner join (
                        select loan_key, max(tax_memo_date) as max_memo
                        from {_tblTaxArrears}
                        group by loan_key
                    ) latest
                        on ta.loan_key = latest.loan_key
                       and ta.tax_memo_date = latest.max_memo
                    group by ta.loan_key
                ) tax on l.loan_key = tax.loan_key

                """;
        }

        private static string BuildOtherCostsExpression(DashboardColumnMap columns)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(columns.OutstandingInvoice))
            {
                parts.Add($"isnull(l.{columns.OutstandingInvoice}, 0)");
            }

            if (!string.IsNullOrEmpty(columns.EstimatedRealization))
            {
                parts.Add($"isnull(l.{columns.EstimatedRealization}, 0)");
            }

            if (!string.IsNullOrEmpty(columns.CostToComplete))
            {
                parts.Add($"isnull(l.{columns.CostToComplete}, 0)");
            }

            return parts.Count == 0 ? "cast(0 as decimal(18, 2))" : string.Join(" + ", parts);
        }

        private static DateTime? MinDate(IEnumerable<DateTime?> values)
        {
            DateTime? min = null;
            foreach (var value in values)
            {
                if (!value.HasValue)
                {
                    continue;
                }

                if (!min.HasValue || value.Value < min.Value)
                {
                    min = value;
                }
            }

            return min;
        }

        private static string ColExpr(string? column, string alias, string type = "decimal") =>
            string.IsNullOrEmpty(column)
                ? type switch
                {
                    "int" => $"       {alias} = cast(null as int),",
                    "varchar" => $"       {alias} = cast(null as varchar(200)),",
                    "datetime2" => $"       {alias} = cast(null as datetime2),",
                    _ => $"       {alias} = cast(0 as decimal(18, 2)),"
                }
                : type switch
                {
                    "int" => $"       {alias} = l.{column},",
                    "varchar" => $"       {alias} = l.{column},",
                    "datetime2" => $"       {alias} = l.{column},",
                    _ => $"       {alias} = isnull(l.{column}, 0),"
                };

        private static LoanSnapshotRow MapLoanSnapshotRow(SqlDataReader reader, DashboardColumnMap columns)
        {
            var principal = GetNullableDecimal(reader, "principal_balance") ?? 0m;
            var osInt = GetNullableDecimal(reader, "outstanding_interest") ?? 0m;
            var accrued = GetNullableDecimal(reader, "accrued_interest") ?? 0m;
            var lateInt = GetNullableDecimal(reader, "late_interest") ?? 0m;
            var defInt = GetNullableDecimal(reader, "default_interest") ?? 0m;
            var intAdj = GetNullableDecimal(reader, "interest_adjustment") ?? 0m;
            var intAdv = GetNullableDecimal(reader, "interest_advance") ?? 0m;
            var tax = GetNullableDecimal(reader, "tax_arrears_amount") ?? 0m;
            var other = GetNullableDecimal(reader, "other_costs") ?? 0m;

            return new LoanSnapshotRow
            {
                LoanKey = GetInt64(reader, "loan_key"),
                LoanCode = GetString(reader, "loan_code"),
                LoanDesc = GetString(reader, "loan_desc"),
                LoanAliasKey = GetInt32(reader, "loan_alias_key"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                InvestorAliasName = GetString(reader, "investor_alias_name"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Units = GetNullableInt32(reader, "units"),
                SquareFeet = GetNullableDecimal(reader, "square_feet"),
                Acres = GetNullableDecimal(reader, "acres"),
                Principal = principal,
                OutstandingInterest = osInt,
                AccruedInterest = accrued,
                LateInterest = lateInt,
                InterestDisbursed = GetNullableDecimal(reader, "interest_disbursed") ?? 0m,
                InterestNotDisbursed = GetNullableDecimal(reader, "interest_not_disbursed") ?? 0m,
                DefaultInterest = defInt,
                InterestAdjustment = intAdj,
                InterestAdvance = intAdv,
                TaxArrears = tax,
                OtherCosts = other,
                TotalExposure = principal + defInt + osInt + accrued + lateInt + intAdj + tax + other,
                Ltv = GetNullableDecimal(reader, "ltv_value"),
                MonthsInArrears = GetNullableInt32(reader, "months_in_arrears"),
                TimesNsfd = GetNullableInt32(reader, "times_nsfd"),
                InterestReserve = GetNullableDecimal(reader, "interest_reserve") ?? 0m,
                InterestReserveBalance = GetNullableDecimal(reader, "interest_reserve_balance") ?? 0m,
                Sponsor = GetNullableString(reader, "sponsor"),
                PropertyAddress = GetNullableString(reader, "property_address"),
                PropertyType = GetNullableString(reader, "property_type"),
                LoanType = GetNullableString(reader, "loan_type"),
                InterestRate = GetNullableDecimal(reader, "interest_rate"),
                Ranking = GetNullableInt32(reader, "loan_ranking"),
                MaturityDate = GetNullableDateTime(reader, "maturity_date"),
                DefaultDate = GetNullableDateTime(reader, "default_date"),
                ExitPlan = GetNullableString(reader, "exit_plan"),
                ExitDate = GetNullableString(reader, "exit_date"),
                SmfInterestStatus = GetNullableString(reader, "smf_interest_status"),
                MlpInterestStatus = GetNullableString(reader, "mlp_interest_status"),
                FundingStatusName = GetString(reader, "funding_status_name"),
                LoanStatusName = GetString(reader, "loan_status_name"),
                DefaultSubjectiveStatus = GetNullableString(reader, "default_subjective_status"),
                ParentLoanId = GetString(reader, "parent_loan_id")
            };
        }

        private static List<LoanAliasSummaryRowDto> BuildLoanAliasSummaryRows(
            IReadOnlyList<LoanSnapshotRow> loanRows,
            DashboardColumnMap columns)
        {
            return loanRows
                .GroupBy(r => r.LoanAliasKey)
                .Select(g =>
                {
                    var principal = g.Sum(r => r.Principal);
                    var ltv = ComputeWeightedLtv(g);
                    return new LoanAliasSummaryRowDto
                    {
                        LoanAliasKey = g.Key,
                        LoanAlias = g.First().LoanAliasName,
                        Sponsor = g.Select(r => r.Sponsor).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                        DefaultDate = MinDate(g.Select(r => r.DefaultDate)),
                        MaturityDate = MinDate(g.Select(r => r.MaturityDate)),
                        InterestStatus = BuildInterestStatus(g),
                        Units = BuildUnitsLabel(
                            g.First().Units,
                            g.First().SquareFeet,
                            g.First().Acres),
                        Exit = BuildExitLabel(g),
                        Security = g.Max(r => r.SecurityValue),
                        Principal = principal,
                        OsInt = g.Sum(r => r.OutstandingInterest),
                        Accrued = g.Sum(r => r.AccruedInterest),
                        LateInt = g.Sum(r => r.LateInterest),
                        TaxIns = g.Sum(r => r.TaxArrears),
                        IntAdv = g.Sum(r => r.InterestAdvance),
                        Other = g.Sum(r => r.OtherCosts),
                        TotalExposure = g.Sum(r => r.TotalExposure),
                        Ltv = ltv,
                        Risk = MapRiskBand(ltv)
                    };
                })
                .OrderBy(r => r.LoanAlias)
                .ToList();
        }

        private static IReadOnlyList<LoanSnapshotRow> ApplyLoanRowInvestorFilter(
            IReadOnlyList<LoanSnapshotRow> loanRows,
            IReadOnlyList<string>? investorAliases)
        {
            if (investorAliases is null or { Count: 0 }
                || investorAliases.Any(a => a.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                return loanRows;
            }

            var investors = new HashSet<string>(investorAliases, StringComparer.OrdinalIgnoreCase);
            return loanRows
                .Where(r => !string.IsNullOrWhiteSpace(r.InvestorAliasName)
                    && investors.Contains(r.InvestorAliasName))
                .ToList();
        }

        private static List<LoanAliasSummaryRowDto> ApplyDashboardFilters(
            List<LoanAliasSummaryRowDto> rows,
            ManagementSummaryDashboardQuery query)
        {
            IEnumerable<LoanAliasSummaryRowDto> filtered = rows;

            if (!string.IsNullOrWhiteSpace(query.Sponsor)
                && !query.Sponsor.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r =>
                    r.Sponsor is not null
                    && r.Sponsor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Any(sponsor => sponsor.Equals(query.Sponsor, StringComparison.OrdinalIgnoreCase)));
            }

            if (query.RiskLevels is { Count: > 0 }
                && !query.RiskLevels.Any(r => r.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
            {
                var risks = new HashSet<string>(query.RiskLevels, StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(r => risks.Contains(r.Risk));
            }

            if (query.DefaultDateFrom.HasValue)
            {
                var from = query.DefaultDateFrom.Value.ToDateTime(TimeOnly.MinValue);
                filtered = filtered.Where(r => r.DefaultDate.HasValue && r.DefaultDate.Value.Date >= from);
            }

            if (query.DefaultDateTo.HasValue)
            {
                var to = query.DefaultDateTo.Value.ToDateTime(TimeOnly.MinValue);
                filtered = filtered.Where(r => r.DefaultDate.HasValue && r.DefaultDate.Value.Date <= to);
            }

            if (query.MaturityDateFrom.HasValue)
            {
                var from = query.MaturityDateFrom.Value.ToDateTime(TimeOnly.MinValue);
                filtered = filtered.Where(r => r.MaturityDate.HasValue && r.MaturityDate.Value.Date >= from);
            }

            if (query.MaturityDateTo.HasValue)
            {
                var to = query.MaturityDateTo.Value.ToDateTime(TimeOnly.MinValue);
                filtered = filtered.Where(r => r.MaturityDate.HasValue && r.MaturityDate.Value.Date <= to);
            }

            return filtered.ToList();
        }

        private static ManagementSummaryKpisDto BuildKpis(
            IReadOnlyList<LoanSnapshotRow> loanRows,
            IReadOnlyList<LoanAliasSummaryRowDto> aliasRows,
            DashboardColumnMap columns)
        {
            var defaultedLoans = loanRows.Where(IsDefaultedLoan).ToList();

            var totalPrincipal = loanRows.Sum(r => r.Principal);
            var defaultedPrincipal = defaultedLoans.Sum(r => r.Principal);
            var aliasLtvs = aliasRows.Where(r => r.Ltv.HasValue).Select(r => r.Ltv!.Value).ToList();

            return new ManagementSummaryKpisDto
            {
                NumberOfLoans = defaultedLoans.Count,
                TotalOutstandingBalance = totalPrincipal,
                AverageLtv = aliasLtvs.Count > 0 ? Math.Round(aliasLtvs.Average(), 2) : null,
                PercentOfFundings = totalPrincipal > 0
                    ? Math.Round(defaultedPrincipal / totalPrincipal * 100m, 1)
                    : null,
                AverageLtvTrendLabel = null,
                MaxLtv = aliasLtvs.Count > 0 ? aliasLtvs.Max() : null
            };
        }

        private static readonly HashSet<string> DashboardStatusLabels =
            new(StringComparer.OrdinalIgnoreCase) { "In Default", "Watchlist", "Performing" };

        private static bool TryParseDashboardStatuses(
            IReadOnlyList<string>? statuses,
            out HashSet<string>? labels)
        {
            labels = null;
            if (statuses is null or { Count: 0 })
            {
                return false;
            }

            var normalized = statuses
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim())
                .ToList();

            if (normalized.Count == 0
                || normalized.Any(status => status.Equals("All", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!normalized.All(DashboardStatusLabels.Contains))
            {
                return false;
            }

            labels = new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        private static bool MatchesDashboardStatus(LoanSnapshotRow row, HashSet<string> labels)
        {
            var isDefaulted = IsDefaultedLoan(row);
            var isWatchlist = IsWatchlistLoan(row);

            if (labels.Contains("In Default") && isDefaulted)
            {
                return true;
            }

            if (labels.Contains("Watchlist") && isWatchlist)
            {
                return true;
            }

            return labels.Contains("Performing") && !isDefaulted && !isWatchlist;
        }

        private static bool IsWatchlistLoan(LoanSnapshotRow row)
        {
            if (row.DefaultSubjectiveStatus is not null
                && row.DefaultSubjectiveStatus.Contains("watch", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return row.LoanStatusName.Contains("watch", StringComparison.OrdinalIgnoreCase)
                || row.FundingStatusName.Contains("watch", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefaultedLoan(LoanSnapshotRow row)
        {
            if (row.FundingStatusName.Equals(DefaultedStatusName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (row.LoanStatusName.Contains("default", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return row.DefaultSubjectiveStatus is not null
                && row.DefaultSubjectiveStatus.Contains("default", StringComparison.OrdinalIgnoreCase);
        }

        private static ManagementSummaryChartsPhase2Dto BuildPhase2Charts(
            IReadOnlyList<LoanAliasSummaryRowDto> aliasRows,
            IReadOnlyList<ChartSliceDto> investorSummary)
        {
            var totalExposure = aliasRows.Sum(r => r.TotalExposure);

            var ltvRiskDistribution = aliasRows
                .GroupBy(r => r.Risk)
                .Select(g => new ChartSliceDto
                {
                    Label = g.Key,
                    Value = g.Count(),
                    SharePercent = aliasRows.Count > 0
                        ? Math.Round((decimal)g.Count() / aliasRows.Count * 100m, 1)
                        : null
                })
                .OrderByDescending(c => c.Value)
                .ToList();

            var top5Exposures = aliasRows
                .OrderByDescending(r => r.TotalExposure)
                .Take(5)
                .Select(r => new ChartSliceDto
                {
                    Label = r.LoanAlias,
                    Value = r.TotalExposure,
                    SharePercent = totalExposure > 0
                        ? Math.Round(r.TotalExposure / totalExposure * 100m, 1)
                        : null
                })
                .ToList();

            var exposureBreakdown = new List<ChartSliceDto>
            {
                Slice("Principal", aliasRows.Sum(r => r.Principal), totalExposure),
                Slice("Outstanding Interest", aliasRows.Sum(r => r.OsInt), totalExposure),
                Slice("Accrued", aliasRows.Sum(r => r.Accrued), totalExposure),
                Slice("Late Interest", aliasRows.Sum(r => r.LateInt), totalExposure),
                Slice("Tax/Insurance", aliasRows.Sum(r => r.TaxIns), totalExposure),
                Slice("Other", aliasRows.Sum(r => r.Other), totalExposure)
            }.Where(c => c.Value > 0).ToList();

            var exposureAnalysis = aliasRows
                .Where(r => r.Ltv.HasValue)
                .Select(r => new ChartSliceDto
                {
                    Label = r.LoanAlias,
                    Value = r.Ltv!.Value,
                    SharePercent = r.TotalExposure > 0 && totalExposure > 0
                        ? Math.Round(r.TotalExposure / totalExposure * 100m, 1)
                        : null
                })
                .OrderByDescending(c => c.Value)
                .Take(10)
                .ToList();

            var sponsorSummary = aliasRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Sponsor) ? "(Unknown)" : r.Sponsor!)
                .Select(g => new ChartSliceDto
                {
                    Label = g.Key,
                    Value = g.Sum(r => r.TotalExposure),
                    SharePercent = totalExposure > 0
                        ? Math.Round(g.Sum(r => r.TotalExposure) / totalExposure * 100m, 1)
                        : null
                })
                .OrderByDescending(c => c.Value)
                .ToList();

            return new ManagementSummaryChartsPhase2Dto
            {
                LtvRiskDistribution = ltvRiskDistribution,
                Top5Exposures = top5Exposures,
                ExposureBreakdown = exposureBreakdown,
                ExposureAnalysis = exposureAnalysis,
                InvestorSummary = investorSummary,
                SponsorSummary = sponsorSummary
            };
        }

        private static ManagementSummaryOutstandingInterestDto BuildOutstandingInterest(
            IReadOnlyList<LoanSnapshotRow> loanRows) =>
            new()
            {
                InterestDisbursed = loanRows.Sum(r => r.InterestDisbursed),
                InterestNotDisbursed = loanRows.Sum(r => r.InterestNotDisbursed),
                TotalOutstandingInterest = loanRows.Sum(r => r.OutstandingInterest),
                TotalLateInterest = loanRows.Sum(r => r.LateInterest)
            };

        private static List<LoanPortfolioDetailRowDto> BuildPortfolioDetailRows(IReadOnlyList<LoanSnapshotRow> loanRows) =>
            loanRows
                .OrderBy(r => r.Ranking ?? int.MaxValue)
                .ThenBy(r => r.LoanCode)
                .Select(r => new LoanPortfolioDetailRowDto
                {
                    LoanId = r.LoanCode,
                    Description = r.LoanDesc,
                    Investor = r.InvestorAliasName,
                    Rank = r.Ranking,
                    Rate = r.InterestRate,
                    Principal = r.Principal,
                    DefInterest = r.DefaultInterest,
                    AccruedInt = r.AccruedInterest,
                    LateInt = r.LateInterest,
                    IntAdj = r.InterestAdjustment,
                    TaxArrears = r.TaxArrears,
                    OtherCosts = r.OtherCosts,
                    TotalExposure = r.TotalExposure,
                    Ltv = r.Ltv,
                    MonthsInArrears = r.MonthsInArrears,
                    TimesNsfd = r.TimesNsfd
                })
                .ToList();

        private static LoanPortfolioDetailTotalsDto BuildPortfolioTotals(
            IReadOnlyList<LoanPortfolioDetailRowDto> rows) =>
            new()
            {
                Principal = rows.Sum(r => r.Principal),
                DefInterest = rows.Sum(r => r.DefInterest),
                AccruedInt = rows.Sum(r => r.AccruedInt),
                LateInt = rows.Sum(r => r.LateInt),
                IntAdj = rows.Sum(r => r.IntAdj),
                TaxArrears = rows.Sum(r => r.TaxArrears),
                OtherCosts = rows.Sum(r => r.OtherCosts),
                TotalExposure = rows.Sum(r => r.TotalExposure)
            };

        private static (IReadOnlyList<ChartSliceDto> ByInvestor, IReadOnlyList<ChartSliceDto> Composition, IReadOnlyList<ChartSliceDto> InvestorBreakdown)
            BuildExposureCharts(IReadOnlyList<LoanPortfolioDetailRowDto> portfolioRows)
        {
            var total = portfolioRows.Sum(r => r.TotalExposure);

            var byInvestor = portfolioRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Investor) ? "(Unknown)" : r.Investor)
                .Select(g => new ChartSliceDto
                {
                    Label = g.Key,
                    Value = g.Sum(r => r.TotalExposure),
                    SharePercent = total > 0 ? Math.Round(g.Sum(r => r.TotalExposure) / total * 100m, 1) : null
                })
                .OrderByDescending(c => c.Value)
                .ToList();

            var composition = new List<ChartSliceDto>
            {
                Slice("Principal", portfolioRows.Sum(r => r.Principal), total),
                Slice("Default Interest", portfolioRows.Sum(r => r.DefInterest), total),
                Slice("Accrued Interest", portfolioRows.Sum(r => r.AccruedInt), total),
                Slice("Late Interest", portfolioRows.Sum(r => r.LateInt), total),
                Slice("Tax Arrears", portfolioRows.Sum(r => r.TaxArrears), total),
                Slice("Other Costs", portfolioRows.Sum(r => r.OtherCosts), total)
            }.Where(c => c.Value > 0).ToList();

            return (byInvestor, composition, byInvestor);
        }

        private static ChartSliceDto Slice(string label, decimal value, decimal total) =>
            new()
            {
                Label = label,
                Value = value,
                SharePercent = total > 0 ? Math.Round(value / total * 100m, 1) : null
            };

        private async Task<ManagementSummaryFilterOptionsDto> LoadFilterOptionsAsync(
            DashboardColumnMap columns,
            CancellationToken cancellationToken)
        {
            var sponsors = new List<string> { "All" };
            var investors = new List<string> { "All" };
            var statuses = new List<string> { "All" };

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            if (!string.IsNullOrEmpty(columns.Sponsor))
            {
                var sponsorSql = $"""
                    select distinct l.{columns.Sponsor}
                    from {_tblDimLoan} l
                    where l.is_current = 1
                      and (l.is_leaf = 1 or l.is_leaf is null)
                      and l.loan_alias_key is not null
                      and l.{columns.Sponsor} is not null
                      and l.{columns.Sponsor} <> ''
                    order by l.{columns.Sponsor}
                    """;

                try
                {
                    await using var sponsorCmd = new SqlCommand(sponsorSql, connection);
                    await using var sponsorReader = await sponsorCmd.ExecuteReaderAsync(cancellationToken);
                    while (await sponsorReader.ReadAsync(cancellationToken))
                    {
                        var name = sponsorReader.GetString(0).Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            sponsors.Add(name);
                        }
                    }
                }
                catch (SqlException ex) when (ex.Number == 207)
                {
                    _logger.LogDebug("Sponsor filter options skipped for column {Column}.", columns.Sponsor);
                }
            }

            var investorSql = $"""
                select distinct iam.investor_alias_name
                from {_tblDimInvestor} inv
                inner join {_tblInvestorAliasMaster} iam
                    on inv.investor_alias_key = iam.investor_alias_id
                where inv.is_current = 1
                  and iam.investor_alias_name is not null
                  and iam.investor_alias_name <> ''
                order by iam.investor_alias_name
                """;

            await using (var investorCmd = new SqlCommand(investorSql, connection))
            await using (var investorReader = await investorCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await investorReader.ReadAsync(cancellationToken))
                {
                    investors.Add(investorReader.GetString(0).Trim());
                }
            }

            var dimStatuses = await LoadDimStatusFilterLabelsAsync(connection, cancellationToken);
            // LoadDimStatusFilterLabelsAsync already appends All; legacy path pre-seeds All.
            foreach (var status in dimStatuses)
            {
                if (!statuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    statuses.Add(status);
                }
            }

            return new ManagementSummaryFilterOptionsDto
            {
                Sponsors = sponsors,
                InvestorAliases = investors,
                Statuses = statuses
            };
        }

        private async Task<IReadOnlyList<CmhcWatchlistRowDto>> LoadWatchlistRowsAsync(
            IReadOnlyList<int>? loanAliasIds,
            DashboardColumnMap columns,
            CancellationToken cancellationToken)
        {
            try
            {
                var tableRows = await TryLoadWatchlistTableRowsAsync(loanAliasIds, cancellationToken);
                if (tableRows is not null)
                {
                    return tableRows;
                }

                return await BuildWatchlistFromSubjectiveColumns(loanAliasIds, columns, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CMHC default watchlist failed to load; continuing without watchlist rows.");
                return [];
            }
        }

        private async Task<IReadOnlyList<CmhcWatchlistRowDto>?> TryLoadWatchlistTableRowsAsync(
            IReadOnlyList<int>? loanAliasIds,
            CancellationToken cancellationToken)
        {
            if (_watchlistTableAvailable == false)
            {
                return null;
            }

            if (_watchlistTableAvailable != true)
            {
                var probe = $"select top 0 ks_loan_no from {_tblCmhcDefaultWatchlist}";
                try
                {
                    await using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);
                    await using var probeCmd = new SqlCommand(probe, connection);
                    await probeCmd.ExecuteReaderAsync(cancellationToken);
                    _watchlistTableAvailable = true;
                }
                catch (SqlException)
                {
                    _watchlistTableAvailable = false;
                    return null;
                }
            }

            // Source: {BronzeLakehouse}.external_files.cmhc_default (Dev: shortcut_lh_bronze1).
            var sql = $"""
                select [ks_loan_no],
                       [aggregator_investor],
                       [sponsor],
                       [property_address],
                       [pmts_missed],
                       [principal_balance],
                       [p_i_arrears],
                       [tax_arrears_as_at_date],
                       [stabilized_ltv_as_at_date],
                       [in_place_dsc_as_at_date],
                       [comments],
                       [report_date],
                       [created_by],
                       [updated_by],
                       [created_datetime],
                       [updated_datetime]
                from {_tblCmhcDefaultWatchlist}
                order by [report_date] desc, [ks_loan_no]
                """;

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, conn);

                var rows = new List<CmhcWatchlistRowDto>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(new CmhcWatchlistRowDto
                    {
                        LoanId = GetString(reader, "ks_loan_no"),
                        Investor = GetString(reader, "aggregator_investor"),
                        Sponsor = GetString(reader, "sponsor"),
                        Property = GetString(reader, "property_address"),
                        Missed = FormatWatchlistCell(reader, "pmts_missed"),
                        Principal = ReadFlexibleDecimal(reader, "principal_balance"),
                        OsInterest = ReadFlexibleDecimal(reader, "p_i_arrears"),
                        TaxArrears = FormatWatchlistCell(reader, "tax_arrears_as_at_date"),
                        Ltv = FormatWatchlistCell(reader, "stabilized_ltv_as_at_date"),
                        Dscr = FormatWatchlistCell(reader, "in_place_dsc_as_at_date"),
                        Issue = GetNullableString(reader, "comments"),
                        StatusUpdate = null,
                        Conclusion = null,
                        Status = "NO CONCERNS",
                        ReportDate = ReadFlexibleDateTime(reader, "report_date")
                    });
                }

                return rows;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed reading {Table}; watchlist will be empty until the bronze cmhc_default schema matches.",
                    _tblCmhcDefaultWatchlist);
                return [];
            }
        }

        private async Task<IReadOnlyList<CmhcWatchlistRowDto>> BuildWatchlistFromSubjectiveColumns(
            IReadOnlyList<int>? loanAliasIds,
            DashboardColumnMap columns,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(columns.WatchlistIssue)
                && string.IsNullOrEmpty(columns.WatchlistConclusion)
                && string.IsNullOrEmpty(columns.WatchlistStatus))
            {
                return [];
            }

            var sql = new StringBuilder();
            sql.AppendLine("select l.loan_code,");
            sql.AppendLine("       investor_alias_name = isnull(iam.investor_alias_name, ''),");
            sql.AppendLine(ColExpr(columns.Sponsor, "sponsor", "varchar"));
            sql.AppendLine("       property = l.loan_desc,");
            sql.AppendLine(ColExpr(columns.WatchlistMissed, "missed", "varchar"));
            sql.AppendLine(string.IsNullOrEmpty(columns.Principal)
                ? "       principal = cast(0 as decimal(18, 2)),"
                : $"       principal = isnull(l.{columns.Principal}, 0),");
            sql.AppendLine(ColExpr(columns.OutstandingInterest, "os_interest"));
            sql.AppendLine("       tax_arrears = cast(isnull(tax.tax_arrears, 0) as varchar(50)),");
            sql.AppendLine(ColExpr(columns.WatchlistDscr, "dscr", "varchar"));
            sql.AppendLine(ColExpr(columns.WatchlistIssue, "issue", "varchar"));
            sql.AppendLine(ColExpr(columns.WatchlistStatusUpdate, "status_update", "varchar"));
            sql.AppendLine(ColExpr(columns.WatchlistConclusion, "conclusion", "varchar"));
            sql.AppendLine(ColExpr(columns.WatchlistStatus, "watch_status", "varchar"));
            sql.AppendLine("       ltv_value = coalesce(lv.ltv, lv.ai_ltv)");
            sql.Append(await BuildSnapshotFromClauseAsync(
                columns,
                columns.ParentLoanKey,
                columns.LoanStatusKey,
                cancellationToken));
            sql.Append(LoanEligibleWhere);
            sql.AppendLine("  and (");
            sql.AppendLine(columns.WatchlistIssue is not null
                ? $"l.{columns.WatchlistIssue} is not null or "
                : string.Empty);
            sql.AppendLine(columns.WatchlistStatus is not null
                ? $"l.{columns.WatchlistStatus} is not null"
                : "1 = 0");
            sql.AppendLine(")");
            AppendLoanAliasFilter(sql, loanAliasIds);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql.ToString(), connection);
            AddLoanAliasParameters(command, loanAliasIds);

            var rows = new List<CmhcWatchlistRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ltv = GetNullableDecimal(reader, "ltv_value");
                rows.Add(new CmhcWatchlistRowDto
                {
                    LoanId = GetString(reader, "loan_code"),
                    Investor = GetString(reader, "investor_alias_name"),
                    Sponsor = GetNullableString(reader, "sponsor") ?? string.Empty,
                    Property = GetString(reader, "property"),
                    Missed = GetNullableString(reader, "missed"),
                    Principal = GetNullableDecimal(reader, "principal"),
                    OsInterest = GetNullableDecimal(reader, "os_interest"),
                    TaxArrears = GetNullableString(reader, "tax_arrears"),
                    Ltv = ltv.HasValue ? $"{ltv:0.##}%" : null,
                    Dscr = GetNullableString(reader, "dscr"),
                    Issue = GetNullableString(reader, "issue"),
                    StatusUpdate = GetNullableString(reader, "status_update"),
                    Conclusion = GetNullableString(reader, "conclusion"),
                    Status = NormalizeWatchlistStatus(GetNullableString(reader, "watch_status") ?? string.Empty)
                });
            }

            return rows;
        }

        private async Task<(DateTime? AsAt, IReadOnlyList<TaxArrearsByYearDto> ByYear)> LoadTaxArrearsForAliasAsync(
            int loanAliasKey,
            CancellationToken cancellationToken)
        {
            await EnsureTaxArrearsTableAvailableAsync(cancellationToken);
            if (_taxArrearsTableAvailable != true)
            {
                return (null, []);
            }

            var sql = $"""
                with alias_loans as (
                    select l.loan_key
                    from {_tblDimLoan} l
                    where l.is_current = 1
                      and (l.is_leaf = 1 or l.is_leaf is null)
                      and l.loan_alias_key = @loan_alias_key
                ),
                latest_memo as (
                    select max(ta.tax_memo_date) as tax_memo_date
                    from {_tblTaxArrears} ta
                    inner join alias_loans al on ta.loan_key = al.loan_key
                )
                select ta.tax_year,
                       sum(ta.tax_arrears) as tax_arrears,
                       max(ta.tax_memo_date) as tax_memo_date
                from {_tblTaxArrears} ta
                inner join alias_loans al on ta.loan_key = al.loan_key
                cross join latest_memo lm
                where ta.tax_memo_date = lm.tax_memo_date
                group by ta.tax_year
                order by ta.tax_year desc
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_alias_key", loanAliasKey);

            DateTime? asAt = null;
            var byYear = new List<TaxArrearsByYearDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                asAt ??= GetNullableDateTime(reader, "tax_memo_date");
                byYear.Add(new TaxArrearsByYearDto
                {
                    Year = GetInt32(reader, "tax_year"),
                    TaxArrears = GetNullableDecimal(reader, "tax_arrears") ?? 0m
                });
            }

            return (asAt, byYear);
        }

        private async Task EnsureTaxArrearsTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_taxArrearsTableAvailable.HasValue)
            {
                return;
            }

            var probe = $"select top 0 loan_key from {_tblTaxArrears}";
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(probe, connection);
                await command.ExecuteReaderAsync(cancellationToken);
                _taxArrearsTableAvailable = true;
            }
            catch (SqlException)
            {
                _taxArrearsTableAvailable = false;
            }
        }

        private static string BuildReportPeriodLabel(DateOnly asOfDate)
        {
            var quarter = (asOfDate.Month - 1) / 3 + 1;
            return $"Q{quarter} {asOfDate.Year}";
        }

        private static string? BuildInterestStatus(IEnumerable<LoanSnapshotRow> rows)
        {
            var parts = new List<string>();
            var smf = rows.Select(r => r.SmfInterestStatus).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            var mlp = rows.Select(r => r.MlpInterestStatus).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            if (!string.IsNullOrWhiteSpace(smf))
            {
                parts.Add($"SMF: {smf}");
            }

            if (!string.IsNullOrWhiteSpace(mlp))
            {
                parts.Add($"MLP: {mlp}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }

        private static string? BuildExitLabel(IEnumerable<LoanSnapshotRow> rows)
        {
            var plan = rows.Select(r => r.ExitPlan).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            var date = rows.Select(r => r.ExitDate).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            if (string.IsNullOrWhiteSpace(plan) && string.IsNullOrWhiteSpace(date))
            {
                return null;
            }

            return string.Join(" — ", new[] { plan, date }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string? BuildUnitsLabel(int? units, decimal? squareFeet, decimal? acres)
        {
            var parts = new List<string>();
            if (units.HasValue)
            {
                parts.Add($"Units: {units}");
            }

            if (squareFeet.HasValue)
            {
                parts.Add($"SF: {squareFeet:0}");
            }

            if (acres.HasValue)
            {
                parts.Add($"Acres: {acres:0.##}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }

        private static decimal? ComputeWeightedLtv(IEnumerable<LoanSnapshotRow> rows)
        {
            var list = rows.ToList();
            var weighted = list
                .Where(r => r.Ltv.HasValue && r.Principal > 0)
                .ToList();

            if (weighted.Count > 0)
            {
                var principalSum = weighted.Sum(r => r.Principal);
                return principalSum > 0
                    ? Math.Round(weighted.Sum(r => r.Ltv!.Value * r.Principal) / principalSum, 2)
                    : null;
            }

            var ltvs = list.Select(r => r.Ltv).Where(l => l.HasValue).Select(l => l!.Value).ToList();
            return ltvs.Count > 0 ? Math.Round(ltvs.Average(), 2) : null;
        }

        private static string MapRiskBand(decimal? ltv)
        {
            if (!ltv.HasValue)
            {
                return "LOW";
            }

            return ltv.Value switch
            {
                > 100m => "HIGH",
                > 75m => "ELEVATED",
                > 60m => "MODERATE",
                _ => "LOW"
            };
        }

        private static string? FormatWatchlistCell(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd"),
                DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd"),
                decimal or double or float or int or long => Convert.ToDecimal(value).ToString("G29"),
                _ => Convert.ToString(value)?.Trim()
            };
        }

        private static decimal? ReadFlexibleDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            if (value is decimal numeric)
            {
                return numeric;
            }

            return decimal.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
        }

        private static DateTime? ReadFlexibleDateTime(SqlDataReader reader, string name)
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

        private static string NormalizeWatchlistStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "NO CONCERNS";
            }

            return status.Contains("CONCERN", StringComparison.OrdinalIgnoreCase)
                && !status.Contains("NO", StringComparison.OrdinalIgnoreCase)
                ? "CONCERN"
                : status.ToUpperInvariant();
        }

        private sealed class DashboardColumnMap
        {
            public string? Principal { get; init; }
            public string? AccrualPostedDate { get; init; }
            public string? FundingStatusKey { get; init; }
            public string? InterestRate { get; init; }
            public string? Sponsor { get; init; }
            public string? PropertyAddress { get; init; }
            public string? PropertyType { get; init; }
            public string? LoanType { get; init; }
            public string? OutstandingInterest { get; init; }
            public string? AccruedInterest { get; init; }
            public string? LateInterest { get; init; }
            public string? InterestDisbursed { get; init; }
            public string? InterestNotDisbursed { get; init; }
            public string? DefaultInterest { get; init; }
            public string? InterestAdjustment { get; init; }
            public string? InterestAdvance { get; init; }
            public string? OutstandingInvoice { get; init; }
            public string? EstimatedRealization { get; init; }
            public string? CostToComplete { get; init; }
            public string? MonthsInArrears { get; init; }
            public string? TimesNsfd { get; init; }
            public string? InterestReserve { get; init; }
            public string? InterestReserveBalance { get; init; }
            public string? SmfInterestStatus { get; init; }
            public string? MlpInterestStatus { get; init; }
            public string? MaturityDate { get; init; }
            public string? ExitPlan { get; init; }
            public string? ExitDate { get; init; }
            public string? DefaultDate { get; init; }
            public string? Exposure { get; init; }
            public string? DimLoanLtv { get; init; }
            public string? ParentLoanKey { get; init; }
            public string? LoanStatusKey { get; init; }
            public string? WatchlistIssue { get; init; }
            public string? WatchlistConclusion { get; init; }
            public string? WatchlistStatusUpdate { get; init; }
            public string? WatchlistDscr { get; init; }
            public string? WatchlistMissed { get; init; }
            public string? WatchlistStatus { get; init; }
            public string? DefaultSubjectiveStatus { get; init; }
        }

        private sealed class LoanSnapshotRow
        {
            public long LoanKey { get; init; }
            public string LoanCode { get; init; } = string.Empty;
            public string LoanDesc { get; init; } = string.Empty;
            public int LoanAliasKey { get; init; }
            public string LoanAliasName { get; init; } = string.Empty;
            public string InvestorAliasName { get; init; } = string.Empty;
            public decimal? SecurityValue { get; init; }
            public int? Units { get; init; }
            public decimal? SquareFeet { get; init; }
            public decimal? Acres { get; init; }
            public decimal Principal { get; init; }
            public decimal OutstandingInterest { get; init; }
            public decimal AccruedInterest { get; init; }
            public decimal LateInterest { get; init; }
            public decimal InterestDisbursed { get; init; }
            public decimal InterestNotDisbursed { get; init; }
            public decimal DefaultInterest { get; init; }
            public decimal InterestAdjustment { get; init; }
            public decimal InterestAdvance { get; init; }
            public decimal TaxArrears { get; init; }
            public decimal OtherCosts { get; init; }
            public decimal TotalExposure { get; init; }
            public decimal? Ltv { get; init; }
            public int? MonthsInArrears { get; init; }
            public int? TimesNsfd { get; init; }
            public decimal InterestReserve { get; init; }
            public decimal InterestReserveBalance { get; init; }
            public string? Sponsor { get; init; }
            public string? PropertyAddress { get; init; }
            public string? PropertyType { get; init; }
            public string? LoanType { get; init; }
            public decimal? InterestRate { get; init; }
            public int? Ranking { get; init; }
            public DateTime? MaturityDate { get; init; }
            public DateTime? DefaultDate { get; init; }
            public string? ExitPlan { get; init; }
            public string? ExitDate { get; init; }
            public string? SmfInterestStatus { get; init; }
            public string? MlpInterestStatus { get; init; }
            public string FundingStatusName { get; init; } = string.Empty;
            public string LoanStatusName { get; init; } = string.Empty;
            public string? DefaultSubjectiveStatus { get; init; }
            public string ParentLoanId { get; init; } = string.Empty;
        }
    }
}
