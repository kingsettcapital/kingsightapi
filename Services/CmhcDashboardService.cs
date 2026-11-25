using System.Globalization;
using System.Text.Json;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace kingsightapi.Services
{
    public interface ICmhcDashboardService
    {
        Task<CmhcPipelineDto> GetPipelineAsync(string? asOf, CancellationToken cancellationToken = default);

        Task<CmhcReportStateDto> GetStateAsync(string key, CancellationToken cancellationToken = default);

        Task<CmhcReportStateWriteResult> SaveStateAsync(
            string key,
            JsonElement value,
            long expectedVersion,
            string? updatedBy,
            CancellationToken cancellationToken = default);
    }

    public sealed class CmhcDashboardService : ICmhcDashboardService
    {
        public static readonly IReadOnlySet<string> AllowedStateKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "hist", "alloc-locked", "alloc-weights", "alloc-workdays", "tl-hidden",
            // v7 team inputs (each mirrors one of the report's own settings; see cmhc-data.service.ts)
            "alloc-coverage", "alloc-active", "alloc-active-reset", "rev-vacations", "rev-cal-capacity",
            "renewal-milestones", "sponsor-pairs", "wip-marks", "wip-credits", "ffa-stamps"
        };

        private const string DealIdColumn = "DP Deal ID";
        private const string DateFormat = "yyyy-MM-dd";

        // Column aliases match the report's field-mapping labels; do not rename.
        private const string PipelineSql = """
            SELECT
                 m.loan_no                                    AS [Loan No]
                ,d.dp_deal_id                                 AS [DP Deal ID]
                ,d.deal_name                                  AS [Deal Name]
                ,m.underwriter                                AS [Underwriter]
                ,m.funder                                     AS [Funder]
                ,m.reviewer                                   AS [Reviewer]
                ,d.originating_originator                     AS [Originating Originator]
                ,d.deal_status                                AS [Deal Status]
                ,CAST(m.initial_loan_funded_date AS date)     AS [Funding Date]
                ,m.loan_amount                                AS [Gross Loan]
                ,m.cmb                                        AS [CMB]
                ,CAST(m.mbs_date AS date)                     AS [MBS Date]
                ,CAST(d.quote_accepted_date AS date)          AS [Quote Accepted Date]
                ,d.site_visited                               AS [Site Visited]
                ,d.status_update                              AS [Status Update]
                ,m.coip_status                                AS [COIP Status]
                ,d.sponsor                                    AS [Sponsor]
                ,m.investor                                   AS [Investor]
                ,m.term_yrs                                   AS [Term (Yrs)]
                ,m.loan_purpose                               AS [Loan Purpose]
                ,m.affordable                                 AS [Affordable]
                ,m.otp_approved                               AS [OTP Approved?]
                ,d.city_rollup + COALESCE(', ' + d.state_rollup, '')
                                                              AS [City, State, ZIP Code]
                ,m.cmhc_application_purpose                   AS [CMHC Application Purpose]
                ,m.coi_source                                 AS [COI Source]
                ,m.form_of_coip                               AS [Form of COIP]
                ,d.asset_stage                                AS [Asset Stage]
                ,d.housing_type                               AS [Housing Type]
                ,d.mixed_use                                  AS [Mixed Use]
                ,m.program_type                               AS [Program Type]
                ,d.source_type                                AS [Source Type]
                -- v7 Renewals / Maturities fields. Keep [Current Originator] after [Originating Originator]:
                -- the report's column mapping matches "originator" by first column containing it.
                ,m.current_originator                         AS [Current Originator]
                ,TRY_CAST(m.loan_maturity_date AS date)       AS [Loan Maturity Date]   -- nvarchar; some rows say "Various"
                ,m.renewal_status                             AS [Renewal Status]
                ,m.renewal_commentary                         AS [Renewal Commentary]
                ,m.loan_type                                  AS [Priority]
                ,m.loan_stage                                 AS [Loan Stage]
                ,m.first_mortgage_lender                      AS [1st Mortgage Lender]
                ,m.first_mortgage_amount                      AS [1st Mortgage Amount]
                ,m.first_mortgage_interest_rate               AS [1st Mortgage Interest Rate]
                ,CAST(m.first_mortgage_maturity_date AS date) AS [1st Mortgage Maturity Date]
            FROM dbo.DealTbl AS d
            INNER JOIN dbo.MasterDealTbl AS m
                    ON m.dp_deal_id = d.dp_deal_id
                   AND m.is_active  = 1
            WHERE d.is_active   = 1
              AND d.deal_type   = 'CMHC'
              AND d.deal_status IN ('Funded', 'Repaid', 'Quote Accepted', 'Commitment Signed', 'Rate Locked')
            ORDER BY d.deal_status, m.initial_loan_funded_date, d.deal_name;
            """;

        private const string SelectStateSql = """
            SELECT JsonValue, Version, UpdatedAt, UpdatedBy
            FROM dbo.ReportStateTbl
            WHERE StateKey = @key;
            """;

        private const string InsertStateSql = """
            INSERT INTO dbo.ReportStateTbl (StateKey, JsonValue, Version, UpdatedAt, UpdatedBy)
            VALUES (@key, @json, 1, SYSDATETIMEOFFSET(), @by);
            """;

        private const string UpdateStateSql = """
            UPDATE dbo.ReportStateTbl
               SET JsonValue = @json,
                   Version   = Version + 1,
                   UpdatedAt = SYSDATETIMEOFFSET(),
                   UpdatedBy = @by
             WHERE StateKey = @key
               AND Version  = @version;
            """;

        private readonly string _connectionString;
        private readonly CmhcDashboardOptions _options;
        private readonly ILogger<CmhcDashboardService> _logger;

        public CmhcDashboardService(
            IConfiguration configuration,
            IOptions<CmhcDashboardOptions> options,
            ILogger<CmhcDashboardService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _options = options.Value;
            _logger = logger;
        }

        public async Task<CmhcPipelineDto> GetPipelineAsync(string? asOf, CancellationToken cancellationToken = default)
        {
            var resolvedAsOf = ResolveAsOf(asOf);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(PipelineSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = new string[reader.FieldCount];
            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = reader.GetName(i);
            }

            var dealIdIndex = Array.IndexOf(columns, DealIdColumn);
            var rows = new List<object?[]>();
            var links = new List<string?>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object?[columns.Length];
                for (var i = 0; i < columns.Length; i++)
                {
                    row[i] = NormalizeValue(reader, i);
                }

                rows.Add(row);
                links.Add(BuildDealLink(dealIdIndex >= 0 ? row[dealIdIndex] : null));
            }

            _logger.LogInformation("CMHC pipeline loaded: {RowCount} rows as of {AsOf}", rows.Count, resolvedAsOf);

            return new CmhcPipelineDto
            {
                AsOf = resolvedAsOf,
                GeneratedAt = DateTimeOffset.UtcNow,
                Columns = columns,
                Rows = rows,
                Links = links
            };
        }

        /// <summary>
        /// CMHC sub-role filter (ACC-01/03/06/08): Admin and Reviewer keep every row; otherwise a row stays when
        /// the user is its underwriter (Funder, falling back to Underwriter — the name the dashboard shows)
        /// or its originator (Current Originator, falling back to Originating Originator). A user with no matching role or no linked name gets no rows.
        /// </summary>
        public static CmhcPipelineDto FilterForAccess(CmhcPipelineDto pipeline, CmhcMyAccessDto access)
        {
            if (access.SeesAllLoans)
            {
                return pipeline;
            }

            var linked = access.LinkedName?.Trim();
            var columns = pipeline.Columns.ToList();
            var funderIdx = columns.IndexOf("Funder");
            var underwriterIdx = columns.IndexOf("Underwriter");
            var currentOriginatorIdx = columns.IndexOf("Current Originator");
            var originatorIdx = columns.IndexOf("Originating Originator");

            var rows = new List<object?[]>();
            var links = new List<string?>();
            if (access.HasAccess && !string.IsNullOrEmpty(linked))
            {
                for (var i = 0; i < pipeline.Rows.Count; i++)
                {
                    var row = pipeline.Rows[i];
                    var funder = CleanName(Cell(row, funderIdx));
                    var effectiveUnderwriter = funder ?? CleanName(Cell(row, underwriterIdx));
                    // Current-first, like the report: Originating Originator only when Current is blank.
                    var originator = CleanName(Cell(row, currentOriginatorIdx)) ?? CleanName(Cell(row, originatorIdx));

                    var keep = (access.IsUnderwriter && SameName(effectiveUnderwriter, linked))
                        || (access.IsOriginator && SameName(originator, linked));
                    if (keep)
                    {
                        rows.Add(row);
                        links.Add(i < pipeline.Links.Count ? pipeline.Links[i] : null);
                    }
                }
            }

            return new CmhcPipelineDto
            {
                AsOf = pipeline.AsOf,
                GeneratedAt = pipeline.GeneratedAt,
                Columns = pipeline.Columns,
                Rows = rows,
                Links = links
            };
        }

        private static object? Cell(object?[] row, int index) => index >= 0 && index < row.Length ? row[index] : null;

        /// <summary>Same rule as the dashboard's cleanStr: blank or "nan" means no name.</summary>
        private static string? CleanName(object? value)
        {
            var s = value?.ToString()?.Trim();
            return string.IsNullOrEmpty(s) || s.Equals("nan", StringComparison.OrdinalIgnoreCase) ? null : s;
        }

        private static bool SameName(string? a, string b) =>
            a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        public async Task<CmhcReportStateDto> GetStateAsync(string key, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(SelectStateSql, connection);
            command.Parameters.AddWithValue("@key", key);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return new CmhcReportStateDto { Key = key, Value = null, Version = 0 };
            }

            return new CmhcReportStateDto
            {
                Key = key,
                Value = JsonDocument.Parse(reader.GetString(reader.GetOrdinal("JsonValue"))).RootElement.Clone(),
                Version = reader.GetInt64(reader.GetOrdinal("Version")),
                UpdatedAt = reader.GetDateTimeOffset(reader.GetOrdinal("UpdatedAt")),
                UpdatedBy = reader.GetNullableString("UpdatedBy")
            };
        }

        public async Task<CmhcReportStateWriteResult> SaveStateAsync(
            string key,
            JsonElement value,
            long expectedVersion,
            string? updatedBy,
            CancellationToken cancellationToken = default)
        {
            var json = value.GetRawText();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            int affected;
            if (expectedVersion <= 0)
            {
                await using var insert = new SqlCommand(InsertStateSql, connection);
                AddStateParameters(insert, key, json, updatedBy);
                try
                {
                    affected = await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException ex) when (ex.Number is 2627 or 2601)
                {
                    // Someone else saved this key first.
                    affected = 0;
                }
            }
            else
            {
                await using var update = new SqlCommand(UpdateStateSql, connection);
                AddStateParameters(update, key, json, updatedBy);
                update.Parameters.AddWithValue("@version", expectedVersion);
                affected = await update.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affected == 0)
            {
                return new CmhcReportStateWriteResult
                {
                    Status = CmhcReportStateWriteStatus.Conflict,
                    State = await GetStateAsync(key, cancellationToken)
                };
            }

            return new CmhcReportStateWriteResult
            {
                Status = CmhcReportStateWriteStatus.Saved,
                State = await GetStateAsync(key, cancellationToken)
            };
        }

        private static void AddStateParameters(SqlCommand command, string key, string json, string? updatedBy)
        {
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@json", json);
            command.Parameters.AddWithValue("@by", (object?)updatedBy ?? DBNull.Value);
        }

        private string ResolveAsOf(string? asOf)
        {
            if (!string.IsNullOrWhiteSpace(asOf))
            {
                if (!DateTime.TryParseExact(asOf.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    throw new ArgumentException($"Query parameter 'asOf' must be formatted {DateFormat}.");
                }

                return parsed.ToString(DateFormat, CultureInfo.InvariantCulture);
            }

            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }

            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone)
                .ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        private string? BuildDealLink(object? dealId)
        {
            var id = dealId?.ToString();
            return string.IsNullOrWhiteSpace(id)
                ? null
                : string.Format(CultureInfo.InvariantCulture, _options.DealpathLinkTemplate, id);
        }

        /// <summary>
        /// Real nulls for SQL NULL/blank text, yyyy-MM-dd for dates, JSON numbers for decimals,
        /// Yes/No for bit columns (the report compares against 'Yes'/'No').
        /// </summary>
        private static object? NormalizeValue(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
                DateTime dt => dt.ToString(DateFormat, CultureInfo.InvariantCulture),
                DateOnly d => d.ToString(DateFormat, CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString(DateFormat, CultureInfo.InvariantCulture),
                bool b => b ? "Yes" : "No",
                _ => value
            };
        }
    }
}
