namespace kingsightapi.Services
{
    internal sealed class LtvValidationOptionalColumns
    {
        public string? LtvColumn { get; init; }
        public string? UpdateReason { get; init; }
        public string? UpdateComment { get; init; }
        public string? AiComments { get; init; }
        public string? AiConfidenceScore { get; init; }

        public static async Task<LtvValidationOptionalColumns> ProbeAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            return new LtvValidationOptionalColumns
            {
                LtvColumn = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    ["current_loan_to_value", "loan_to_value", "ltv", "loan_ltv"],
                    cancellationToken),
                UpdateReason = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    ["user_update_reason", "update_reason", "ltv_update_reason"],
                    cancellationToken),
                UpdateComment = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    ["user_update_comments", "user_update_comment", "update_comment", "ltv_update_comment"],
                    cancellationToken),
                AiComments = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    ["ai_comments", "ai_commentary"],
                    cancellationToken),
                AiConfidenceScore = await DimLoanColumnProbe.FindFirstAsync(
                    connectionString,
                    tableName,
                    ["ai_confidence_score", "confidence_score", "ai_confidence"],
                    cancellationToken),
            };
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
                SelectAliasOrNull(AiConfidenceScore, relationshipAlias, "ai_confidence_score", "decimal(5, 4)"),
            };

            return ",\n                       " + string.Join(",\n                       ", parts);
        }

        public string BuildLtvSelectExpression(string relationshipAlias = "a") =>
            SelectAliasOrNull(LtvColumn, relationshipAlias, "ltv", "decimal(18, 4)");

        public string BuildSelectFragment(string relationshipAlias = "a") =>
            ",\n                       " + BuildLtvSelectExpression(relationshipAlias)
            + BuildOptionalSelectFragment(relationshipAlias);

        public string BuildLtvIsNotNullCondition(string relationshipAlias = "a") =>
            LtvColumn is null
                ? "1 = 0"
                : $"{relationshipAlias}.{Bracket(LtvColumn)} is not null";

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
