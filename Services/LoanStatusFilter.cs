using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    internal sealed class LoanStatusFilter
    {
        public IReadOnlyList<long> StatusKeys { get; init; } = [];
        public IReadOnlyList<string> StatusNames { get; init; } = [];
        public bool IncludeNull { get; init; }
        public bool HasFilter => StatusKeys.Count > 0 || StatusNames.Count > 0 || IncludeNull;
    }

    internal static class LoanStatusFilterParser
    {
        public static LoanStatusFilter Parse(IReadOnlyList<string>? statuses)
        {
            if (statuses is null or { Count: 0 })
            {
                return new LoanStatusFilter();
            }

            var keys = new List<long>();
            var names = new List<string>();
            var includeNull = false;

            foreach (var raw in statuses)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var token = raw.Trim();
                if (IsNullToken(token))
                {
                    includeNull = true;
                    continue;
                }

                if (long.TryParse(token, out var statusKey))
                {
                    if (!keys.Contains(statusKey))
                    {
                        keys.Add(statusKey);
                    }

                    continue;
                }

                if (!names.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(token);
                }
            }

            return new LoanStatusFilter
            {
                StatusKeys = keys,
                StatusNames = names,
                IncludeNull = includeNull
            };
        }

        public static void AppendSqlCondition(
            StringBuilder sql,
            string loanTableAlias,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable)
        {
            if (!filter.HasFilter)
            {
                return;
            }

            var statusAlias = loanTableAlias switch
            {
                "l" => "s",
                "lf" => "sf",
                _ => "ds"
            };

            if (filter.StatusNames.Count > 0)
            {
                sql.AppendLine();
                sql.Append($"  left join {dimStatusTable} {statusAlias}");
                sql.Append($" on {loanTableAlias}.{loanStatusKeyColumn} = {statusAlias}.status_key");
            }

            var conditions = new List<string>();
            if (filter.StatusKeys.Count > 0)
            {
                var inList = string.Join(
                    ", ",
                    filter.StatusKeys.Select((_, i) => $"@status_key_{i}"));
                conditions.Add($"{loanTableAlias}.{loanStatusKeyColumn} in ({inList})");
            }

            if (filter.StatusNames.Count > 0)
            {
                var inList = string.Join(
                    ", ",
                    filter.StatusNames.Select((_, i) => $"@status_name_{i}"));
                conditions.Add($"{statusAlias}.status_name in ({inList})");
            }

            if (filter.IncludeNull)
            {
                conditions.Add($"{loanTableAlias}.{loanStatusKeyColumn} is null");
            }

            sql.AppendLine();
            sql.Append("  and (");
            sql.Append(string.Join(" or ", conditions));
            sql.Append(')');
        }

        /// <summary>
        /// Status filter via EXISTS — avoids Fabric warehouse errors when adding dim_loan to the FROM clause.
        /// </summary>
        public static void AppendExistsSqlCondition(
            StringBuilder sql,
            string relationshipAlias,
            string sharedDimLoanTable,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable)
        {
            if (!filter.HasFilter)
            {
                return;
            }

            const string dimLoanAlias = "dl";
            var statusAlias = "ds";

            sql.AppendLine();
            sql.Append("  and exists (");
            sql.Append("select 1 from ");
            sql.Append(sharedDimLoanTable);
            sql.Append($" {dimLoanAlias} where ");
            sql.Append(SubjectiveInputSql.EqualsLoanCode(relationshipAlias, "loan_code", dimLoanAlias, "loan_code"));
            sql.Append($" and {SubjectiveInputSql.DimLoanIsCurrent(dimLoanAlias)}");

            if (filter.StatusNames.Count > 0)
            {
                sql.Append($" left join {dimStatusTable} {statusAlias}");
                sql.Append($" on {dimLoanAlias}.{loanStatusKeyColumn} = {statusAlias}.status_key");
            }

            var conditions = new List<string>();
            if (filter.StatusKeys.Count > 0)
            {
                var inList = string.Join(
                    ", ",
                    filter.StatusKeys.Select((_, i) => $"@status_key_{i}"));
                conditions.Add($"{dimLoanAlias}.{loanStatusKeyColumn} in ({inList})");
            }

            if (filter.StatusNames.Count > 0)
            {
                var inList = string.Join(
                    ", ",
                    filter.StatusNames.Select((_, i) => $"@status_name_{i}"));
                conditions.Add($"{statusAlias}.status_name in ({inList})");
            }

            if (filter.IncludeNull)
            {
                conditions.Add($"{dimLoanAlias}.{loanStatusKeyColumn} is null");
            }

            sql.Append(" and (");
            sql.Append(string.Join(" or ", conditions));
            sql.Append("))");
        }

        public static void AddParameters(SqlCommand command, LoanStatusFilter filter)
        {
            for (var i = 0; i < filter.StatusKeys.Count; i++)
            {
                command.Parameters.AddWithValue($"@status_key_{i}", filter.StatusKeys[i]);
            }

            for (var i = 0; i < filter.StatusNames.Count; i++)
            {
                command.Parameters.AddWithValue($"@status_name_{i}", filter.StatusNames[i]);
            }
        }

        private static bool IsNullToken(string token) =>
            token.Equals(LoanSecurityValueStatusTokens.NullValue, StringComparison.OrdinalIgnoreCase)
            || token.Equals("__NULL__", StringComparison.OrdinalIgnoreCase);
    }
}
