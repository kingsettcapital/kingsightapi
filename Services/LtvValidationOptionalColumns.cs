using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    /// <summary>
    /// Optional LTV Validation columns on subjective_input.loan_alias_relationship.
    /// Matches warehouse fields: current_loan_to_value, prior_loan_to_value, user_update_*, ai_comments, etc.
    /// </summary>
    internal sealed class LtvValidationOptionalColumns
    {
        public string? LtvColumn { get; init; }
        public string? PriorLtvColumn { get; init; }
        public string? UpdateReason { get; init; }
        public string? UpdateComment { get; init; }
        public string? AiComments { get; init; }
        public string? AiConfidenceScore { get; init; }
        /// <summary>Confirm LTV flag on relationship (<c>is_confirmed = 'Y'</c>).</summary>
        public string? IsConfirmedColumn { get; init; }
        /// <summary>FK to <c>file_upload_history.file_id</c> for As Of header dates.</summary>
        public string? FileUploadIdColumn { get; init; }

        public static async Task<LtvValidationOptionalColumns> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var columns = await LoadColumnNamesAsync(connectionString, tableName, cancellationToken);

            // Current LTV — prefer explicit current_* then legacy names.
            var currentLtvColumn = FindColumn(
                columns,
                "current_loan_to_value",
                "loan_to_value",
                "ltv",
                "loan_ltv");

            // Prior LTV — prefer prior_loan_to_value (warehouse standard).
            // Legacy: when both current_loan_to_value and loan_to_value exist, loan_to_value was prior.
            var priorLtvColumn = FindColumn(columns, "prior_loan_to_value");
            if (priorLtvColumn is null
                && currentLtvColumn is not null
                && string.Equals(currentLtvColumn, "current_loan_to_value", StringComparison.OrdinalIgnoreCase)
                && FindColumn(columns, "loan_to_value") is { } legacyPrior)
            {
                priorLtvColumn = legacyPrior;
            }

            return new LtvValidationOptionalColumns
            {
                LtvColumn = currentLtvColumn,
                PriorLtvColumn = priorLtvColumn,
                UpdateReason = FindColumn(
                    columns,
                    "user_update_reason",
                    "update_reason",
                    "ltv_update_reason"),
                UpdateComment = FindColumn(
                    columns,
                    "user_update_comments",
                    "user_update_comment",
                    "update_comment",
                    "ltv_update_comment"),
                AiComments = FindColumn(columns, "ai_comments", "ai_commentary"),
                AiConfidenceScore = FindColumn(
                    columns,
                    "ai_confidence_score",
                    "confidence_score",
                    "ai_confidence"),
                IsConfirmedColumn = FindColumn(
                    columns,
                    "is_confirmed",
                    "ltv_is_confirmed",
                    "is_ltv_confirmed"),
                FileUploadIdColumn = FindColumn(columns, "file_upload_id"),
            };
        }

        public string BuildConfirmUpdateSetClause(string relationshipAlias = "a")
        {
            var sets = new List<string>();
            if (IsConfirmedColumn is not null)
            {
                // Warehouse flag used by reports: is_confirmed = 'Y'.
                sets.Add($"{relationshipAlias}.{Bracket(IsConfirmedColumn)} = 'Y'");
            }

            return sets.Count == 0
                ? string.Empty
                : string.Join(",\n                    ", sets);
        }

        public string BuildUpdateSetClause(string relationshipAlias = "a")
        {
            var ltvSet = BuildLtvUpdateSetClause(relationshipAlias);
            var optionalSet = BuildOptionalUpdateSetClause(relationshipAlias);
            if (string.IsNullOrEmpty(ltvSet))
            {
                return optionalSet;
            }

            return optionalSet.Length == 0 ? ltvSet : ltvSet + optionalSet;
        }

        public string BuildLtvUpdateSetClause(string relationshipAlias = "a")
        {
            if (LtvColumn is null)
            {
                return string.Empty;
            }

            return $"{relationshipAlias}.{Bracket(LtvColumn)} = @ltv";
        }

        public string BuildOptionalUpdateSetClause(string relationshipAlias = "a")
        {
            var sets = new List<string>();
            AddSet(sets, UpdateReason, relationshipAlias, "@update_reason");
            AddSet(sets, UpdateComment, relationshipAlias, "@update_comment");
            return sets.Count == 0 ? string.Empty : ",\n                    " + string.Join(",\n                    ", sets);
        }

        public string BuildOptionalSelectFragment(string relationshipAlias = "a")
        {
            var parts = new List<string>
            {
                SelectAliasOrNull(UpdateReason, relationshipAlias, "update_reason", "varchar(500)"),
                SelectAliasOrNull(UpdateComment, relationshipAlias, "update_comment", "varchar(500)"),
                SelectAliasOrNull(AiComments, relationshipAlias, "ai_comments", "varchar(max)"),
                SelectAliasOrNull(AiConfidenceScore, relationshipAlias, "ai_confidence_score", "decimal(18, 4)"),
            };

            return ",\n                       " + string.Join(",\n                       ", parts);
        }

        public string BuildLtvSelectExpression(string relationshipAlias = "a") =>
            SelectAliasOrNull(LtvColumn, relationshipAlias, "ltv", "decimal(18, 4)");

        public string BuildPriorLtvSelectExpression(string relationshipAlias = "a") =>
            SelectAliasOrNull(PriorLtvColumn, relationshipAlias, "prior_ltv", "decimal(18, 4)");

        public string BuildSelectFragment(string relationshipAlias = "a") =>
            ",\n                       " + BuildLtvSelectExpression(relationshipAlias)
            + ",\n                       " + BuildPriorLtvSelectExpression(relationshipAlias)
            + BuildOptionalSelectFragment(relationshipAlias);

        public string BuildLtvIsNotNullCondition(string relationshipAlias = "a") =>
            LtvColumn is null
                ? "1 = 0"
                : $"{relationshipAlias}.{Bracket(LtvColumn)} is not null";

        private static async Task<HashSet<string>> LoadColumnNamesAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand($"select top (0) * from {tableName}", connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    names.Add(reader.GetName(i));
                }
            }
            catch (SqlException)
            {
                // Fall back to per-candidate probes below if top-0 * is blocked.
            }

            if (names.Count > 0)
            {
                return names;
            }

            // Fallback: individual candidate probes (legacy path).
            foreach (var candidate in new[]
                     {
                         "current_loan_to_value", "prior_loan_to_value", "loan_to_value", "ltv", "loan_ltv",
                         "user_update_reason", "update_reason", "ltv_update_reason",
                         "user_update_comments", "user_update_comment", "update_comment", "ltv_update_comment",
                         "ai_comments", "ai_commentary",
                         "ai_confidence_score", "confidence_score", "ai_confidence",
                         "is_confirmed", "ltv_is_confirmed", "is_ltv_confirmed",
                     })
            {
                var found = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    [candidate],
                    cancellationToken);
                if (found is not null)
                {
                    names.Add(found);
                }
            }

            return names;
        }

        private static string? FindColumn(HashSet<string> columns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.TryGetValue(candidate, out var actual))
                {
                    return actual;
                }

                // TryGetValue with OrdinalIgnoreCase comparer returns the stored casing.
                foreach (var existing in columns)
                {
                    if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return existing;
                    }
                }
            }

            return null;
        }

        private static void AddSet(List<string> sets, string? column, string alias, string parameter)
        {
            if (column is not null)
            {
                sets.Add($"{alias}.{Bracket(column)} = {parameter}");
            }
        }

        private static string SelectAliasOrNull(
            string? column,
            string alias,
            string resultAlias,
            string sqlType) =>
            column is null
                ? $"cast(null as {sqlType}) as {resultAlias}"
                : $"{alias}.{Bracket(column)} as {resultAlias}";

        private static string Bracket(string column) => $"[{column}]";
    }
}
