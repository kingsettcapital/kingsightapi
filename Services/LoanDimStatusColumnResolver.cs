using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    internal static class LoanDimStatusColumnResolver
    {
        private static readonly string[] ColumnCandidates =
        [
            "loan_status_key",
            "status_key",
            "loan_status_id",
            "status_id",
            "funding_status_key"
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
                "mort.dim_loan does not have a recognized status foreign key column. "
                + $"Expected one of: {string.Join(", ", ColumnCandidates)}.");
        }
    }
}
