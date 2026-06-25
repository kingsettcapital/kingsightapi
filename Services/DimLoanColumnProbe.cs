using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    internal static class DimLoanColumnProbe
    {
        public static async Task<string?> FindFirstAsync(
            string connectionString,
            string dimLoanTable,
            IReadOnlyList<string> columnCandidates,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var column in columnCandidates)
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

            return null;
        }
    }
}
