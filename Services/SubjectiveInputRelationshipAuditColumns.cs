using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    /// <summary>
    /// Updated-by / updated-datetime columns on subjective_input relationship tables.
    /// </summary>
    internal sealed class SubjectiveInputRelationshipAuditColumns
    {
        public string? UpdatedByColumn { get; private init; }
        public string? UpdatedDtmColumn { get; private init; }

        public static async Task<SubjectiveInputRelationshipAuditColumns> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var updatedBy = await DimLoanColumnProbe.FindFirstAsync(
                connectionString,
                tableName,
                [
                    "ltv_updated_by",
                    "default_si_updated_by",
                    "updated_by",
                    "user_updated_by",
                    "modified_by",
                ],
                cancellationToken);
            var updatedDtm = await DimLoanColumnProbe.FindFirstAsync(
                connectionString,
                tableName,
                [
                    "ltv_updated_datetime",
                    "default_si_updated_datetime",
                    "updated_datetime",
                    "updated_dtm",
                    "updated_date",
                    "user_updated_date",
                    "modified_at",
                    "modified_date",
                ],
                cancellationToken);

            return new SubjectiveInputRelationshipAuditColumns
            {
                UpdatedByColumn = updatedBy,
                UpdatedDtmColumn = updatedDtm
            };
        }

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
