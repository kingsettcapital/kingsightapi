using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    internal static class LoanDimStatusColumnResolver
    {
        /// <summary>
        /// Probe order for dim_loan status FK columns.
        /// wh_gold1.shared.dim_loan uses <c>funding_status_code</c> → shared.dim_status.status_key.
        /// </summary>
        private static readonly string[] ColumnCandidates =
        [
            "funding_status_code",
            "loan_status_key",
            "status_key",
            "loan_status_id",
            "status_id",
            "funding_status_key",
        ];

        public static async Task<string> ResolveAsync(
            string connectionString,
            string dimLoanTable,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var column in ColumnCandidates)
            {
                var probeSql = $"select top 0 [{column}] from {dimLoanTable}";

                try
                {
                    await using var command = new SqlCommand(probeSql, connection);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    return column;
                }
                catch (SqlException ex) when (ex.Number == 207)
                {
                    // Try next candidate.
                }
            }

            throw new InvalidOperationException(
                $"{dimLoanTable} does not have a recognized status foreign key column. "
                + $"Expected one of: {string.Join(", ", ColumnCandidates)}.");
        }
    }
}
