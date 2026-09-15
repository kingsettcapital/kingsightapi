using System.Data;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IFundSharePointDocumentsStore
{
    Task<FundSharePointLibraryRow?> GetLibraryAsync(
        int fundKey,
        string category,
        CancellationToken cancellationToken = default);

    Task UpsertLibraryAsync(
        FundSharePointLibraryRow row,
        string auditUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FundDocumentItemDto>> GetCachedDocumentsAsync(
        int fundKey,
        string category,
        CancellationToken cancellationToken = default);

    Task ReplaceCachedDocumentsAsync(
        int fundKey,
        string category,
        IReadOnlyList<FundDocumentItemDto> items,
        CancellationToken cancellationToken = default);
}

public sealed class FundSharePointDocumentsStore : IFundSharePointDocumentsStore
{
    private readonly string _connectionString;
    private readonly string _libraryTable;
    private readonly string _documentTable;
    private readonly ILogger<FundSharePointDocumentsStore> _logger;

    public FundSharePointDocumentsStore(
        IConfiguration configuration,
        ILogger<FundSharePointDocumentsStore> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _libraryTable = WarehouseTables.FundSharePointLibrary;
        _documentTable = WarehouseTables.FundDocument;
        _logger = logger;
    }

    public async Task<FundSharePointLibraryRow?> GetLibraryAsync(
        int fundKey,
        string category,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            $"""
            select top (1)
                fund_key,
                fund_code,
                category,
                sharepoint_url,
                site_url,
                library_server_relative_url,
                folder_server_relative_url,
                library_title,
                is_active
            from {_libraryTable}
            where fund_key = @fund_key
              and category = @category
              and upper(isnull(is_active, 'Y')) in ('Y', '1', 'T')
            order by updated_datetime desc, created_datetime desc
            """,
            connection);

        command.Parameters.Add("@fund_key", SqlDbType.Int).Value = fundKey;
        command.Parameters.Add("@category", SqlDbType.VarChar, 200).Value = category;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new FundSharePointLibraryRow
            {
                FundKey = reader.GetInt32(reader.GetOrdinal("fund_key")),
                FundCode = reader.IsDBNull(reader.GetOrdinal("fund_code"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("fund_code")),
                Category = reader.GetString(reader.GetOrdinal("category")),
                SharePointUrl = reader.GetString(reader.GetOrdinal("sharepoint_url")),
                SiteUrl = reader.IsDBNull(reader.GetOrdinal("site_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("site_url")),
                LibraryServerRelativeUrl = reader.IsDBNull(reader.GetOrdinal("library_server_relative_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("library_server_relative_url")),
                FolderServerRelativeUrl = reader.IsDBNull(reader.GetOrdinal("folder_server_relative_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("folder_server_relative_url")),
                LibraryTitle = reader.IsDBNull(reader.GetOrdinal("library_title"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("library_title")),
                IsActive = true,
            };
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read {Table}. Run Scripts/Create_investor_servicing_fund_sharepoint_documents.sql.",
                _libraryTable);
            return null;
        }
    }

    public async Task UpsertLibraryAsync(
        FundSharePointLibraryRow row,
        string auditUser,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var update = new SqlCommand(
            $"""
            update {_libraryTable}
            set fund_code = @fund_code,
                sharepoint_url = @sharepoint_url,
                site_url = @site_url,
                library_server_relative_url = @library_server_relative_url,
                folder_server_relative_url = @folder_server_relative_url,
                library_title = @library_title,
                is_active = 'Y',
                updated_by = @audit_user,
                updated_datetime = @now
            where fund_key = @fund_key
              and category = @category
            """,
            connection);

        BindLibraryParams(update, row, auditUser, now);
        var affected = await update.ExecuteNonQueryAsync(cancellationToken);
        if (affected > 0)
        {
            return;
        }

        await using var insert = new SqlCommand(
            $"""
            insert into {_libraryTable} (
                fund_key, fund_code, category, sharepoint_url,
                site_url, library_server_relative_url, folder_server_relative_url, library_title,
                is_active, notes, created_by, created_datetime, updated_by, updated_datetime
            )
            values (
                @fund_key, @fund_code, @category, @sharepoint_url,
                @site_url, @library_server_relative_url, @folder_server_relative_url, @library_title,
                'Y', null, @audit_user, @now, @audit_user, @now
            )
            """,
            connection);

        BindLibraryParams(insert, row, auditUser, now);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FundDocumentItemDto>> GetCachedDocumentsAsync(
        int fundKey,
        string category,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            $"""
            select
                sharepoint_item_id,
                file_name,
                quarter,
                year,
                modified_on,
                modified_by,
                size_bytes,
                web_url,
                server_relative_url
            from {_documentTable}
            where fund_key = @fund_key
              and isnull(category, @category) = @category
              and upper(isnull(is_active, 'Y')) in ('Y', '1', 'T')
            order by year desc, quarter desc, modified_on desc, file_name
            """,
            connection);

        command.Parameters.Add("@fund_key", SqlDbType.Int).Value = fundKey;
        command.Parameters.Add("@category", SqlDbType.VarChar, 200).Value = category;

        var items = new List<FundDocumentItemDto>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new FundDocumentItemDto
                {
                    Id = reader.GetString(reader.GetOrdinal("sharepoint_item_id")),
                    Name = reader.GetString(reader.GetOrdinal("file_name")),
                    Quarter = reader.IsDBNull(reader.GetOrdinal("quarter"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("quarter")),
                    Year = reader.IsDBNull(reader.GetOrdinal("year"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("year")),
                    ModifiedOn = reader.IsDBNull(reader.GetOrdinal("modified_on"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("modified_on")),
                    ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("modified_by")),
                    SizeBytes = reader.IsDBNull(reader.GetOrdinal("size_bytes"))
                        ? null
                        : reader.GetInt64(reader.GetOrdinal("size_bytes")),
                    WebUrl = reader.IsDBNull(reader.GetOrdinal("web_url"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("web_url")),
                    ServerRelativeUrl = reader.IsDBNull(reader.GetOrdinal("server_relative_url"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("server_relative_url")),
                });
            }
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read cached documents from {Table}.",
                _documentTable);
        }

        return items;
    }

    public async Task ReplaceCachedDocumentsAsync(
        int fundKey,
        string category,
        IReadOnlyList<FundDocumentItemDto> items,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var deactivate = new SqlCommand(
                $"""
                update {_documentTable}
                set is_active = 'N'
                where fund_key = @fund_key
                  and isnull(category, @category) = @category
                """,
                connection,
                transaction))
            {
                deactivate.Parameters.Add("@fund_key", SqlDbType.Int).Value = fundKey;
                deactivate.Parameters.Add("@category", SqlDbType.VarChar, 200).Value = category;
                await deactivate.ExecuteNonQueryAsync(cancellationToken);
            }

            long nextId = await GetNextDocumentIdAsync(connection, transaction, cancellationToken);

            foreach (var item in items)
            {
                await using var insert = new SqlCommand(
                    $"""
                    insert into {_documentTable} (
                        fund_document_id, fund_key, sharepoint_item_id, file_name, category,
                        quarter, year, modified_on, modified_by, size_bytes,
                        web_url, server_relative_url, synced_at, is_active
                    )
                    values (
                        @fund_document_id, @fund_key, @sharepoint_item_id, @file_name, @category,
                        @quarter, @year, @modified_on, @modified_by, @size_bytes,
                        @web_url, @server_relative_url, @synced_at, 'Y'
                    )
                    """,
                    connection,
                    transaction);

                insert.Parameters.Add("@fund_document_id", SqlDbType.BigInt).Value = nextId++;
                insert.Parameters.Add("@fund_key", SqlDbType.Int).Value = fundKey;
                insert.Parameters.Add("@sharepoint_item_id", SqlDbType.VarChar, 64).Value =
                    string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
                insert.Parameters.Add("@file_name", SqlDbType.VarChar, 500).Value = item.Name;
                insert.Parameters.Add("@category", SqlDbType.VarChar, 200).Value = category;
                insert.Parameters.Add("@quarter", SqlDbType.VarChar, 10).Value =
                    (object?)item.Quarter ?? DBNull.Value;
                insert.Parameters.Add("@year", SqlDbType.Int).Value =
                    (object?)item.Year ?? DBNull.Value;
                insert.Parameters.Add("@modified_on", SqlDbType.DateTime2).Value =
                    (object?)item.ModifiedOn ?? DBNull.Value;
                insert.Parameters.Add("@modified_by", SqlDbType.VarChar, 200).Value =
                    (object?)item.ModifiedBy ?? DBNull.Value;
                insert.Parameters.Add("@size_bytes", SqlDbType.BigInt).Value =
                    (object?)item.SizeBytes ?? DBNull.Value;
                insert.Parameters.Add("@web_url", SqlDbType.VarChar, 1000).Value =
                    (object?)item.WebUrl ?? DBNull.Value;
                insert.Parameters.Add("@server_relative_url", SqlDbType.VarChar, 1000).Value =
                    (object?)item.ServerRelativeUrl ?? DBNull.Value;
                insert.Parameters.Add("@synced_at", SqlDbType.DateTime2).Value = now;

                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                // Fabric / SQL may already abort the transaction; never let rollback mask the root error
                // or throw out to the caller (live SharePoint results should still be returned).
                if (transaction.Connection != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
            }
            catch (Exception rollbackEx)
            {
                _logger.LogDebug(
                    rollbackEx,
                    "Rollback after document cache refresh failure for fund {FundKey}",
                    fundKey);
            }

            _logger.LogWarning(
                ex,
                "Failed to refresh document cache for fund {FundKey} in {Table}. Live SharePoint results still returned.",
                fundKey,
                _documentTable);
        }
    }

    private async Task<long> GetNextDocumentIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            $"select isnull(max(fund_document_id), 0) + 1 from {_documentTable}",
            connection,
            transaction);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long l ? l : Convert.ToInt64(result);
    }

    private static void BindLibraryParams(
        SqlCommand command,
        FundSharePointLibraryRow row,
        string auditUser,
        DateTime now)
    {
        command.Parameters.Add("@fund_key", SqlDbType.Int).Value = row.FundKey;
        command.Parameters.Add("@fund_code", SqlDbType.VarChar, 50).Value =
            (object?)row.FundCode ?? DBNull.Value;
        command.Parameters.Add("@category", SqlDbType.VarChar, 200).Value = row.Category;
        command.Parameters.Add("@sharepoint_url", SqlDbType.VarChar, 1000).Value = row.SharePointUrl;
        command.Parameters.Add("@site_url", SqlDbType.VarChar, 500).Value =
            (object?)row.SiteUrl ?? DBNull.Value;
        command.Parameters.Add("@library_server_relative_url", SqlDbType.VarChar, 500).Value =
            (object?)row.LibraryServerRelativeUrl ?? DBNull.Value;
        command.Parameters.Add("@folder_server_relative_url", SqlDbType.VarChar, 500).Value =
            (object?)row.FolderServerRelativeUrl ?? DBNull.Value;
        command.Parameters.Add("@library_title", SqlDbType.VarChar, 200).Value =
            (object?)row.LibraryTitle ?? DBNull.Value;
        command.Parameters.Add("@audit_user", SqlDbType.VarChar, 150).Value = auditUser;
        command.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
    }
}
