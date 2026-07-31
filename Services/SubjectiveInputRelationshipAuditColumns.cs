using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    /// <summary>
    /// Screen-specific updated-by / updated-datetime columns on subjective_input relationship tables.
    /// Each capture/assignment screen must read and write only its own pair.
    /// </summary>
    internal enum SubjectiveInputAuditScreen
    {
        LoanAlias,
        LoanAttribute,
        OtherCost,
        DefaultDate,
        DefaultSi,
        Ltv,
        /// <summary>Generic fallback (investor relationship, tax arrears, bridges).</summary>
        Generic,
    }

    internal sealed class SubjectiveInputRelationshipAuditColumns
    {
        public string? UpdatedByColumn { get; private init; }
        public string? UpdatedDtmColumn { get; private init; }
        public SubjectiveInputAuditScreen Screen { get; private init; }

        /// <summary>Legacy shared probe — prefer <see cref="ProbeForScreenAsync"/>.</summary>
        public static Task<SubjectiveInputRelationshipAuditColumns> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default) =>
            ProbeForScreenAsync(connectionString, tableName, SubjectiveInputAuditScreen.Generic, cancellationToken);

        public static async Task<SubjectiveInputRelationshipAuditColumns> ProbeForScreenAsync(
            string connectionString,
            string tableName,
            SubjectiveInputAuditScreen screen,
            CancellationToken cancellationToken = default)
        {
            var (byCandidates, dtmCandidates) = CandidatesFor(screen);

            var updatedBy = await DimLoanColumnProbe.FindFirstAsync(
                connectionString,
                tableName,
                byCandidates,
                cancellationToken);
            var updatedDtm = await DimLoanColumnProbe.FindFirstAsync(
                connectionString,
                tableName,
                dtmCandidates,
                cancellationToken);

            return new SubjectiveInputRelationshipAuditColumns
            {
                UpdatedByColumn = updatedBy,
                UpdatedDtmColumn = updatedDtm,
                Screen = screen,
            };
        }

        /// <summary>
        /// Preferred screen columns first, then legacy generic only.
        /// Never fall through to another screen's columns.
        /// </summary>
        private static (string[] By, string[] Dtm) CandidatesFor(SubjectiveInputAuditScreen screen) =>
            screen switch
            {
                SubjectiveInputAuditScreen.LoanAlias => (
                    ["loan_alias_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["loan_alias_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                SubjectiveInputAuditScreen.LoanAttribute => (
                    ["loan_attribute_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["loan_attribute_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                SubjectiveInputAuditScreen.OtherCost => (
                    ["other_cost_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["other_cost_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                SubjectiveInputAuditScreen.DefaultDate => (
                    ["default_date_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["default_date_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                SubjectiveInputAuditScreen.DefaultSi => (
                    ["default_si_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["default_si_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                SubjectiveInputAuditScreen.Ltv => (
                    ["ltv_updated_by", "updated_by", "user_updated_by", "modified_by"],
                    ["ltv_updated_datetime", "updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
                _ => (
                    ["updated_by", "user_updated_by", "modified_by"],
                    ["updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"]),
            };

        public string BuildSelectUpdatedByExpression(string relationshipAlias = "r")
        {
            if (UpdatedByColumn is null)
            {
                return "''";
            }

            return $"isnull({relationshipAlias}.{Bracket(UpdatedByColumn)}, '')";
        }

        public string BuildSelectUpdatedDtmExpression(string relationshipAlias = "r")
        {
            if (UpdatedDtmColumn is null)
            {
                return "cast(null as datetime2)";
            }

            return $"{relationshipAlias}.{Bracket(UpdatedDtmColumn)}";
        }

        public string BuildUpdateSetClause()
        {
            var sets = new List<string>();
            if (UpdatedByColumn is not null)
            {
                sets.Add($"{Bracket(UpdatedByColumn)} = @audit_user");
            }

            if (UpdatedDtmColumn is not null)
            {
                sets.Add($"{Bracket(UpdatedDtmColumn)} = @audit_dtm");
            }

            return sets.Count == 0 ? string.Empty : ", " + string.Join(", ", sets);
        }

        public (IReadOnlyList<string> Columns, IReadOnlyList<string> Values) BuildInsertColumnList()
        {
            var columns = new List<string>();
            var values = new List<string>();
            if (UpdatedByColumn is not null)
            {
                columns.Add(Bracket(UpdatedByColumn));
                values.Add("@audit_user");
            }

            if (UpdatedDtmColumn is not null)
            {
                columns.Add(Bracket(UpdatedDtmColumn));
                values.Add("@audit_dtm");
            }

            return (columns, values);
        }

        public void AddUpdateParameters(SqlCommand command, string auditDisplayName, DateTime auditUtc)
        {
            if (UpdatedByColumn is not null)
            {
                command.Parameters.AddWithValue("@audit_user", auditDisplayName);
            }

            if (UpdatedDtmColumn is not null)
            {
                command.Parameters.AddWithValue("@audit_dtm", auditUtc);
            }
        }

        private static string Bracket(string column) => $"[{column}]";
    }
}
