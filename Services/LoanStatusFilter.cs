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

        /// <summary>
        /// Status filter on an already-joined <c>shared.dim_loan</c> alias.
        /// Matches each loan via <c>funding_status_code</c> and, when present,
        /// <c>funding_status_description</c> (resolved from <c>dim_status.status_name</c> for selected keys).
        /// </summary>
        public static void AppendJoinedDimLoanStatusCondition(
            StringBuilder sql,
            string dimLoanAlias,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable,
            string? loanStatusDescriptionColumn = null)
        {
            if (!filter.HasFilter)
            {
                return;
            }

            var conditions = BuildLoanStatusMatchConditions(
                dimLoanAlias,
                loanStatusKeyColumn,
                filter,
                dimStatusTable,
                loanStatusDescriptionColumn);

            if (conditions.Count == 0)
            {
                return;
            }

            sql.AppendLine();
            sql.Append("  and (");
            sql.Append(string.Join(" or ", conditions));
            sql.Append(')');
        }

        /// <summary>
        /// Legacy helper used by Security Value (alias-level EXISTS). Prefer
        /// <see cref="AppendJoinedDimLoanStatusCondition"/> for loan-level grids.
        /// </summary>
        public static void AppendSqlCondition(
            StringBuilder sql,
            string loanTableAlias,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable,
            string? loanStatusDescriptionColumn = null)
        {
            AppendJoinedDimLoanStatusCondition(
                sql,
                loanTableAlias,
                loanStatusKeyColumn,
                filter,
                dimStatusTable,
                loanStatusDescriptionColumn);
        }

        /// <summary>
        /// Status filter via EXISTS on current <c>shared.dim_loan</c> rows for a relationship loan_code.
        /// </summary>
        public static void AppendExistsSqlCondition(
            StringBuilder sql,
            string relationshipAlias,
            string sharedDimLoanTable,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable,
            string? loanStatusDescriptionColumn = null,
            string? dimLoanCurrentIndicatorColumn = null)
        {
            if (!filter.HasFilter)
            {
                return;
            }

            const string dimLoanAlias = "dl";
            var conditions = BuildLoanStatusMatchConditions(
                dimLoanAlias,
                loanStatusKeyColumn,
                filter,
                dimStatusTable,
                loanStatusDescriptionColumn);

            if (conditions.Count == 0)
            {
                return;
            }

            sql.AppendLine();
            sql.Append("  and exists (");
            sql.Append("select 1 from ");
            sql.Append(sharedDimLoanTable);
            sql.Append($" {dimLoanAlias} where ");
            sql.Append(SubjectiveInputSql.EqualsVarchar(relationshipAlias, "loan_code", dimLoanAlias, "loan_code"));
            sql.Append($" and {SubjectiveInputSql.DimLoanIsCurrent(dimLoanAlias, dimLoanCurrentIndicatorColumn)}");
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

        private static List<string> BuildLoanStatusMatchConditions(
            string dimLoanAlias,
            string loanStatusKeyColumn,
            LoanStatusFilter filter,
            string dimStatusTable,
            string? loanStatusDescriptionColumn)
        {
            var conditions = new List<string>();
            var hasDescription = !string.IsNullOrWhiteSpace(loanStatusDescriptionColumn);

            if (filter.StatusKeys.Count > 0)
            {
                var keyInList = string.Join(
                    ", ",
                    filter.StatusKeys.Select((_, i) => $"@status_key_{i}"));

                // Dropdown sends dim_status.status_key (e.g. Default = 2).
                conditions.Add($"{dimLoanAlias}.[{loanStatusKeyColumn}] in ({keyInList})");

                // Also match funding_status_description to that key's status_name
                // (product mapping: Status → dim_loan.funding_status_description).
                if (hasDescription)
                {
                    conditions.Add(
                        $"{dimLoanAlias}.[{loanStatusDescriptionColumn}] in ("
                        + $"select s.status_name from {dimStatusTable} s "
                        + $"where s.status_key in ({keyInList}))");
                }
            }

            if (filter.StatusNames.Count > 0)
            {
                var nameInList = string.Join(
                    ", ",
                    filter.StatusNames.Select((_, i) => $"@status_name_{i}"));

                conditions.Add(
                    $"exists (select 1 from {dimStatusTable} s "
                    + $"where s.status_name in ({nameInList}) "
                    + $"and {dimLoanAlias}.[{loanStatusKeyColumn}] = s.status_key)");

                if (hasDescription)
                {
                    conditions.Add($"{dimLoanAlias}.[{loanStatusDescriptionColumn}] in ({nameInList})");
                }
            }

            if (filter.IncludeNull)
            {
                conditions.Add($"{dimLoanAlias}.[{loanStatusKeyColumn}] is null");
            }

            return conditions;
        }

        private static bool IsNullToken(string token) =>
            token.Equals(LoanSecurityValueStatusTokens.NullValue, StringComparison.OrdinalIgnoreCase)
            || token.Equals("__NULL__", StringComparison.OrdinalIgnoreCase);
    }
}
