using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class DataExplorerService
{
    public async Task<DataExplorerTemplateDto> SaveTemplateAsync(DataExplorerSaveTemplateRequest request, string? createdBy)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validated = await ValidateTemplateRequestAsync(request);

        var now = DateTime.UtcNow;
        var matchType = DataExplorerFilterSql.NormalizeMatchTypeForStorage(request.FilterLogic);
        var createdByValue = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        if (await TemplateNameExistsAsync(connection, validated.Name, excludeTemplateId: null))
        {
            throw new ArgumentException($"A template named '{validated.Name}' already exists.");
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        long templateId;
        try
        {
            templateId = await InsertTemplateHeaderAsync(
                connection,
                transaction,
                validated.Name,
                validated.Description,
                matchType,
                validated.GroupByColumn?.Name,
                createdByValue,
                now,
                modifiedBy: null,
                modifiedAt: null);

            await InsertTemplateColumnsAsync(connection, transaction, templateId, validated.Columns);
            await InsertTemplateFiltersAsync(connection, transaction, templateId, validated.Filters);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("Saved data explorer template {TemplateId} ({Name}).", templateId, validated.Name);

        return await LoadTemplateAsync(connection, templateId)
            ?? throw new InvalidOperationException("Template was saved but could not be loaded.");
    }

    public async Task<DataExplorerTemplateDto> UpdateTemplateAsync(
        long templateId,
        DataExplorerSaveTemplateRequest request,
        string? modifiedBy)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validated = await ValidateTemplateRequestAsync(request);
        var matchType = DataExplorerFilterSql.NormalizeMatchTypeForStorage(request.FilterLogic);
        var modifiedByValue = string.IsNullOrWhiteSpace(modifiedBy) ? null : modifiedBy.Trim();
        var now = DateTime.UtcNow;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        if (!await TemplateExistsAsync(connection, templateId))
        {
            throw new ArgumentException($"Template '{templateId}' was not found.");
        }

        if (await TemplateNameExistsAsync(connection, validated.Name, templateId))
        {
            throw new ArgumentException($"A template named '{validated.Name}' already exists.");
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await DeleteTemplateChildrenAsync(connection, transaction, templateId);

            const string updateSql =
                " update " + WarehouseTables.DataExplorerTemplate + " set " +
                " template_name = @templateName, " +
                " description = @description, " +
                " match_type = @matchType, " +
                " group_by_field = @groupByField, " +
                " modified_by = @modifiedBy, " +
                " modified_at = @modifiedAt " +
                " where template_id = @templateId and is_active = 1 ";

            await using (var updateCommand = new SqlCommand(updateSql, connection, transaction))
            {
                updateCommand.Parameters.AddWithValue("@templateId", templateId);
                updateCommand.Parameters.AddWithValue("@templateName", validated.Name);
                updateCommand.Parameters.AddWithValue("@description", (object?)validated.Description ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@matchType", matchType);
                updateCommand.Parameters.AddWithValue("@groupByField", (object?)validated.GroupByColumn?.Name ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@modifiedBy", (object?)modifiedByValue ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@modifiedAt", now);

                var rows = await updateCommand.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    throw new ArgumentException($"Template '{templateId}' was not found.");
                }
            }

            await InsertTemplateColumnsAsync(connection, transaction, templateId, validated.Columns);
            await InsertTemplateFiltersAsync(connection, transaction, templateId, validated.Filters);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("Updated data explorer template {TemplateId} ({Name}).", templateId, validated.Name);

        return await LoadTemplateAsync(connection, templateId)
            ?? throw new InvalidOperationException("Template was updated but could not be loaded.");
    }

    public async Task<IReadOnlyList<DataExplorerTemplateSummaryDto>> GetTemplatesAsync()
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" t.template_id, ");
        sql.Append(" t.template_name, ");
        sql.Append(" t.description, ");
        sql.Append(" t.group_by_field, ");
        sql.Append(" t.created_by, ");
        sql.Append(" t.created_at, ");
        sql.Append(" t.modified_at, ");
        sql.Append($" (select count(*) from {WarehouseTables.DataExplorerTemplateColumn} c where c.template_id = t.template_id) as column_count, ");
        sql.Append($" (select count(*) from {WarehouseTables.DataExplorerTemplateFilter} f where f.template_id = t.template_id) as filter_count ");
        sql.Append($" from {WarehouseTables.DataExplorerTemplate} t ");
        sql.Append(" where t.is_active = 1 ");
        sql.Append(" order by t.template_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };

        var items = new List<DataExplorerTemplateSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DataExplorerTemplateSummaryDto
            {
                TemplateId = reader.GetInt64OrDefault("template_id"),
                Name = reader.GetStringOrEmpty("template_name"),
                Description = reader.GetNullableString("description"),
                ColumnCount = reader.GetInt32OrDefault("column_count"),
                FilterCount = reader.GetInt32OrDefault("filter_count"),
                GroupByField = reader.GetNullableString("group_by_field"),
                CreatedBy = reader.GetNullableString("created_by"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                ModifiedAt = reader.GetNullableDateTime("modified_at")
            });
        }

        return items;
    }

    public async Task<DataExplorerTemplateDto?> GetTemplateAsync(long templateId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return await LoadTemplateAsync(connection, templateId);
    }

    public async Task DeleteTemplateAsync(long templateId, string? modifiedBy)
    {
        var modifiedByValue = string.IsNullOrWhiteSpace(modifiedBy) ? null : modifiedBy.Trim();

        const string sql =
            " update " + WarehouseTables.DataExplorerTemplate + " set " +
            " is_active = 0, modified_by = @modifiedBy, modified_at = @modifiedAt " +
            " where template_id = @templateId and is_active = 1 ";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@templateId", templateId);
        command.Parameters.AddWithValue("@modifiedBy", (object?)modifiedByValue ?? DBNull.Value);
        command.Parameters.AddWithValue("@modifiedAt", DateTime.UtcNow);

        var rows = await command.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            throw new ArgumentException($"Template '{templateId}' was not found.");
        }

        _logger.LogInformation("Soft-deleted data explorer template {TemplateId}.", templateId);
    }

    // --- template persistence helpers ----------------------------------------------------------

    private sealed class ValidatedTemplateRequest
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required List<DataExplorerColumnDto> Columns { get; init; }
        public required List<ResolvedDataExplorerFilter> Filters { get; init; }
        public DataExplorerColumnDto? GroupByColumn { get; init; }
    }

    private async Task<ValidatedTemplateRequest> ValidateTemplateRequestAsync(DataExplorerSaveTemplateRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.");
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Template name must be 200 characters or fewer.");
        }

        var catalog = await GetColumnCatalogAsync();
        var byKey = BuildLookup(catalog);

        var columns = ResolveSelectedColumns(request.Columns, byKey);
        if (columns.Count == 0)
        {
            throw new ArgumentException("Select at least one valid column.");
        }

        var filters = ResolveFilters(request.Filters, byKey);

        DataExplorerColumnDto? groupByColumn = null;
        if (!string.IsNullOrWhiteSpace(request.GroupByField))
        {
            if (!byKey.TryGetValue(request.GroupByField.Trim(), out groupByColumn))
            {
                throw new ArgumentException($"Group-by column '{request.GroupByField}' is not a valid column.");
            }
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > 1000)
        {
            throw new ArgumentException("Description must be 1000 characters or fewer.");
        }

        return new ValidatedTemplateRequest
        {
            Name = name,
            Description = description,
            Columns = columns,
            Filters = filters,
            GroupByColumn = groupByColumn
        };
    }

    private static async Task<bool> TemplateNameExistsAsync(
        SqlConnection connection,
        string templateName,
        long? excludeTemplateId)
    {
        const string sql =
            " select count(*) from " + WarehouseTables.DataExplorerTemplate +
            " where is_active = 1 and template_name = @templateName " +
            " and (@excludeTemplateId is null or template_id <> @excludeTemplateId) ";

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@templateName", templateName);
        command.Parameters.AddWithValue("@excludeTemplateId", (object?)excludeTemplateId ?? DBNull.Value);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<bool> TemplateExistsAsync(SqlConnection connection, long templateId)
    {
        const string sql =
            " select count(*) from " + WarehouseTables.DataExplorerTemplate +
            " where template_id = @templateId and is_active = 1 ";

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@templateId", templateId);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }

    /// <summary>Inserts the template header; Fabric assigns template_id via BIGINT IDENTITY.</summary>
    private static async Task<long> InsertTemplateHeaderAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string templateName,
        string? description,
        string matchType,
        string? groupByField,
        string? createdBy,
        DateTime createdAt,
        string? modifiedBy,
        DateTime? modifiedAt)
    {
        // Fabric Warehouse does not support OUTPUT or SCOPE_IDENTITY() on INSERT.
        // template_id is resolved after insert via unique template_name (enforced before insert).
        const string insertSql =
            " insert into " + WarehouseTables.DataExplorerTemplate + " ( " +
            " template_name, description, source_view, match_type, group_by_field, " +
            " created_by, created_at, modified_by, modified_at, is_active " +
            " ) values ( " +
            " @templateName, @description, @sourceView, @matchType, @groupByField, " +
            " @createdBy, @createdAt, @modifiedBy, @modifiedAt, 1 " +
            " ) ";

        await using (var insertCommand = new SqlCommand(insertSql, connection, transaction)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            insertCommand.Parameters.AddWithValue("@templateName", templateName);
            insertCommand.Parameters.AddWithValue("@description", (object?)description ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@sourceView", WarehouseTables.ViewInvestorPortfolioLtdName);
            insertCommand.Parameters.AddWithValue("@matchType", matchType);
            insertCommand.Parameters.AddWithValue("@groupByField", (object?)groupByField ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@createdBy", (object?)createdBy ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@createdAt", createdAt);
            insertCommand.Parameters.AddWithValue("@modifiedBy", (object?)modifiedBy ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@modifiedAt", (object?)modifiedAt ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync();
        }

        // Fabric does not support OUTPUT or SCOPE_IDENTITY() — resolve id by unique template_name.
        const string lookupSql =
            " select template_id from " + WarehouseTables.DataExplorerTemplate +
            " where template_name = @templateName and is_active = 1 ";

        await using var lookupCommand = new SqlCommand(lookupSql, connection, transaction)
        {
            CommandType = System.Data.CommandType.Text
        };
        lookupCommand.Parameters.AddWithValue("@templateName", templateName);
        var lookupResult = await lookupCommand.ExecuteScalarAsync();
        if (lookupResult is null || lookupResult == DBNull.Value)
        {
            throw new InvalidOperationException("Template was inserted but the new template_id could not be resolved.");
        }

        return Convert.ToInt64(lookupResult);
    }

    private static async Task InsertTemplateColumnsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long templateId,
        IReadOnlyList<DataExplorerColumnDto> columns)
    {
        const string sql =
            " insert into " + WarehouseTables.DataExplorerTemplateColumn +
            " (template_id, column_name, display_order) values (@templateId, @columnName, @displayOrder) ";

        for (var i = 0; i < columns.Count; i++)
        {
            await using var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@templateId", templateId);
            command.Parameters.AddWithValue("@columnName", columns[i].Name);
            command.Parameters.AddWithValue("@displayOrder", i + 1);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertTemplateFiltersAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long templateId,
        IReadOnlyList<ResolvedDataExplorerFilter> filters)
    {
        // filter_id is BIGINT IDENTITY — omit on insert.
        const string sql =
            " insert into " + WarehouseTables.DataExplorerTemplateFilter +
            " (template_id, column_name, [operator], filter_value, filter_order) " +
            " values (@templateId, @columnName, @operator, @filterValue, @filterOrder) ";

        for (var i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            await using var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@templateId", templateId);
            command.Parameters.AddWithValue("@columnName", filter.Column.Name);
            command.Parameters.AddWithValue("@operator", filter.Operator);
            command.Parameters.AddWithValue("@filterValue", (object?)filter.Value ?? DBNull.Value);
            command.Parameters.AddWithValue("@filterOrder", i + 1);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteTemplateChildrenAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long templateId)
    {
        const string deleteFilters =
            " delete from " + WarehouseTables.DataExplorerTemplateFilter + " where template_id = @templateId ";
        await using (var command = new SqlCommand(deleteFilters, connection, transaction))
        {
            command.Parameters.AddWithValue("@templateId", templateId);
            await command.ExecuteNonQueryAsync();
        }

        const string deleteColumns =
            " delete from " + WarehouseTables.DataExplorerTemplateColumn + " where template_id = @templateId ";
        await using (var command = new SqlCommand(deleteColumns, connection, transaction))
        {
            command.Parameters.AddWithValue("@templateId", templateId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<DataExplorerTemplateDto?> LoadTemplateAsync(SqlConnection connection, long templateId)
    {
        const string headerSql =
            " select template_id, template_name, description, source_view, match_type, group_by_field, " +
            " created_by, created_at, modified_by, modified_at " +
            " from " + WarehouseTables.DataExplorerTemplate +
            " where template_id = @templateId and is_active = 1 ";

        string templateName;
        string? description;
        string sourceView;
        string matchType;
        string? groupByField;
        string? createdBy;
        DateTime createdAt;
        string? modifiedBy;
        DateTime? modifiedAt;

        await using (var headerCommand = new SqlCommand(headerSql, connection))
        {
            headerCommand.Parameters.AddWithValue("@templateId", templateId);
            await using var reader = await headerCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            templateId = reader.GetInt64OrDefault("template_id");
            templateName = reader.GetStringOrEmpty("template_name");
            description = reader.GetNullableString("description");
            sourceView = reader.GetStringOrEmpty("source_view");
            matchType = reader.GetStringOrEmpty("match_type");
            groupByField = reader.GetNullableString("group_by_field");
            createdBy = reader.GetNullableString("created_by");
            createdAt = reader.GetDateTime(reader.GetOrdinal("created_at"));
            modifiedBy = reader.GetNullableString("modified_by");
            modifiedAt = reader.GetNullableDateTime("modified_at");
        }

        var columns = new List<string>();
        const string columnsSql =
            " select column_name from " + WarehouseTables.DataExplorerTemplateColumn +
            " where template_id = @templateId order by display_order ";
        await using (var columnsCommand = new SqlCommand(columnsSql, connection))
        {
            columnsCommand.Parameters.AddWithValue("@templateId", templateId);
            await using var reader = await columnsCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetStringOrEmpty("column_name"));
            }
        }

        var filters = new List<DataExplorerFilterDto>();
        const string filtersSql =
            " select column_name, [operator], filter_value from " + WarehouseTables.DataExplorerTemplateFilter +
            " where template_id = @templateId order by filter_order ";
        await using (var filtersCommand = new SqlCommand(filtersSql, connection))
        {
            filtersCommand.Parameters.AddWithValue("@templateId", templateId);
            await using var reader = await filtersCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                filters.Add(new DataExplorerFilterDto
                {
                    Field = reader.GetStringOrEmpty("column_name"),
                    Operator = reader.GetStringOrEmpty("operator"),
                    Value = reader.GetNullableString("filter_value")
                });
            }
        }

        return new DataExplorerTemplateDto
        {
            TemplateId = templateId,
            Name = templateName,
            Description = description,
            SourceView = sourceView,
            Columns = columns,
            Filters = filters,
            FilterLogic = matchType.Equals("OR", StringComparison.OrdinalIgnoreCase) ? "or" : "and",
            GroupByField = groupByField,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ModifiedBy = modifiedBy,
            ModifiedAt = modifiedAt
        };
    }
}
