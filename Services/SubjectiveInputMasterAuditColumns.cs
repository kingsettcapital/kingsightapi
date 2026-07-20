using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    /// <summary>
    /// Audit columns on subjective_input.loan_alias_master / investor_alias_master.
    /// Create writes created_by + created_datetime when present; otherwise updated_by + updated_datetime.
    /// Update writes updated_by + updated_datetime only.
    /// </summary>
    internal sealed class SubjectiveInputMasterAuditColumns
    {
        public string? ReadCreatedByColumn { get; private init; }
        public string? ReadUpdatedByColumn { get; private init; }
        public string? ReadCreatedDtmColumn { get; private init; }
        public string? ReadUpdatedDtmColumn { get; private init; }

        private string? InsertCreatedByColumn { get; init; }
        private string? InsertCreatedDtmColumn { get; init; }

        private string? UpdateUpdatedByColumn { get; init; }
        private string? UpdateUpdatedDtmColumn { get; init; }

        public static async Task<SubjectiveInputMasterAuditColumns> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var updatedBy = await FindFirstAsync(
                connectionString,
                tableName,
                ["updated_by", "user_updated_by", "modified_by"],
                cancellationToken);
            var updatedDtm = await FindFirstAsync(
                connectionString,
                tableName,
                ["updated_datetime", "updated_dtm", "updated_date", "user_updated_date", "modified_at", "modified_date"],
                cancellationToken);
            var createdBy = await FindFirstAsync(
                connectionString,
                tableName,
                ["created_by", "user_created_by"],
                cancellationToken);
            var createdDtm = await FindFirstAsync(
                connectionString,
                tableName,
                ["created_datetime", "created_dtm", "created_date", "user_created_date", "created_at"],
                cancellationToken);

            return new SubjectiveInputMasterAuditColumns
            {
                ReadUpdatedByColumn = updatedBy,
                ReadUpdatedDtmColumn = updatedDtm,
                ReadCreatedByColumn = createdBy,
                ReadCreatedDtmColumn = createdDtm,
                InsertCreatedByColumn = createdBy,
                InsertCreatedDtmColumn = createdDtm,
                UpdateUpdatedByColumn = updatedBy,
                UpdateUpdatedDtmColumn = updatedDtm
            };
        }

        public IEnumerable<string> SelectListColumns() =>
            DistinctColumns(ReadCreatedByColumn, ReadCreatedDtmColumn, ReadUpdatedByColumn, ReadUpdatedDtmColumn);

        public string BuildInsertColumnList()
        {
            if (HasCreateAuditColumns())
            {
                var columns = DistinctColumns(InsertCreatedByColumn, InsertCreatedDtmColumn);
                return columns.Count == 0 ? string.Empty : ", " + string.Join(", ", columns);
            }

            var fallback = DistinctColumns(UpdateUpdatedByColumn, UpdateUpdatedDtmColumn);
            return fallback.Count == 0 ? string.Empty : ", " + string.Join(", ", fallback);
        }

        public string BuildInsertValueList()
        {
            var assignments = BuildInsertAssignments();
            return assignments.Count == 0
                ? string.Empty
                : ", " + string.Join(", ", assignments.Select(pair => pair.valueExpression));
        }

        public void AddInsertParameters(SqlCommand command, string auditDisplayName, DateTime auditUtc)
        {
            var addedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assignment in BuildInsertAssignments())
            {
                if (!addedParameters.Add(assignment.parameterName))
                {
                    continue;
                }

                if (assignment.isDate)
                {
                    command.Parameters.AddWithValue(assignment.parameterName, auditUtc);
                }
                else
                {
                    command.Parameters.AddWithValue(assignment.parameterName, auditDisplayName);
                }
            }
        }

        public string BuildUpdateSetClause()
        {
            var sets = new List<string>();
            if (UpdateUpdatedByColumn is not null)
            {
                sets.Add($"{Bracket(UpdateUpdatedByColumn)} = @audit_user");
            }

            if (UpdateUpdatedDtmColumn is not null)
            {
                sets.Add($"{Bracket(UpdateUpdatedDtmColumn)} = @audit_dtm");
            }

            return sets.Count == 0 ? string.Empty : ", " + string.Join(", ", sets);
        }

        public void AddUpdateParameters(SqlCommand command, string auditDisplayName, DateTime auditUtc)
        {
            if (UpdateUpdatedByColumn is not null)
            {
                command.Parameters.AddWithValue("@audit_user", auditDisplayName);
            }

            if (UpdateUpdatedDtmColumn is not null)
            {
                command.Parameters.AddWithValue("@audit_dtm", auditUtc);
            }
        }

        private List<(string parameterName, string valueExpression, bool isDate)> BuildInsertAssignments()
        {
            var assignments = new List<(string parameterName, string valueExpression, bool isDate)>();
            var usedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddColumn(string? column, string parameterName, bool isDate)
            {
                if (string.IsNullOrWhiteSpace(column) || !usedColumns.Add(column))
                {
                    return;
                }

                assignments.Add((parameterName, parameterName, isDate));
            }

            if (HasCreateAuditColumns())
            {
                AddColumn(InsertCreatedByColumn, "@audit_created_by", isDate: false);
                AddColumn(InsertCreatedDtmColumn, "@audit_created_dtm", isDate: true);
                return assignments;
            }

            AddColumn(UpdateUpdatedByColumn, "@audit_user", isDate: false);
            AddColumn(UpdateUpdatedDtmColumn, "@audit_dtm", isDate: true);
            return assignments;
        }

        private bool HasCreateAuditColumns() =>
            InsertCreatedByColumn is not null || InsertCreatedDtmColumn is not null;

        private static async Task<string?> FindFirstAsync(
            string connectionString,
            string tableName,
            IReadOnlyList<string> candidates,
            CancellationToken cancellationToken) =>
            await DimLoanColumnProbe.FindFirstAsync(connectionString, tableName, candidates, cancellationToken);

        private static List<string> DistinctColumns(params string?[] columns)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                if (string.IsNullOrWhiteSpace(column) || !seen.Add(column))
                {
                    continue;
                }

                results.Add(Bracket(column));
            }

            return results;
        }

        private static string Bracket(string column) => $"[{column}]";
    }
}
