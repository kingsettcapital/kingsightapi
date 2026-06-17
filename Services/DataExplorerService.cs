using System.Text;
using System.Text.Json;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>
/// Reads column metadata and data from <c>view_investor_portfolio_ltd</c> for the Data Explorer tool.
/// Column selection is dynamic, so every requested column is validated against the live view schema
/// before it is ever placed into SQL (identifiers are whitelisted; values stay parameterized).
/// </summary>
public sealed partial class DataExplorerService : IDataExplorerService
{
    private readonly string _connectionString;
    private readonly ILogger<DataExplorerService> _logger;

    // The view schema is static at runtime, so cache the column catalog after the first load.
    private readonly SemaphoreSlim _columnsLock = new(1, 1);
    private IReadOnlyList<DataExplorerColumnDto>? _columnsCache;

    // Acronyms that should stay upper-cased in generated labels.
    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "irr", "ltd", "nav", "fmv", "noi", "ltv", "id", "pct", "ytd", "mtd", "qtd", "usd", "cad"
    };

    public DataExplorerService(IConfiguration configuration, ILogger<DataExplorerService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
        _logger.LogInformation(
            "DataExplorerService ready. {ConnectionInfo}",
            ConnectionLogging.Sanitize(_connectionString));
    }

    public async Task<IReadOnlyList<DataExplorerColumnGroupDto>> GetColumnsAsync()
    {
        var columns = await GetColumnCatalogAsync();

        // Keep the group order deterministic: Investors, then Fund, then Capital.
        var groupOrder = new[] { DataExplorerGroups.Investors, DataExplorerGroups.Fund, DataExplorerGroups.Capital };

        return groupOrder
            .Select(group => new DataExplorerColumnGroupDto
            {
                Group = group,
                Columns = columns
                    .Where(c => c.Group == group)
                    .OrderBy(c => c.Ordinal)
                    .ToList()
            })
            .Where(g => g.Columns.Count > 0)
            .ToList();
    }

    public async Task<DataExplorerDataResult> GetDataAsync(DataExplorerDataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var catalog = await GetColumnCatalogAsync();
        var byKey = BuildLookup(catalog);

        var selected = ResolveSelectedColumns(request.Columns, byKey);
        if (selected.Count == 0)
        {
            throw new ArgumentException("Select at least one valid column.");
        }

        var sortColumn = ResolveSortColumn(request.SortBy, byKey, selected);
        var descending = ResolveSortDirection(request.SortDir);
        var (page, pageSize, offset) = Pagination.Normalize(request.Page, request.PageSize);
        var searchTerm = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var filterLogic = DataExplorerFilterSql.NormalizeFilterLogic(request.FilterLogic);
        var resolvedFilters = ResolveFilters(request.Filters, byKey);

        DataExplorerColumnDto? groupByColumn = null;
        if (!string.IsNullOrWhiteSpace(request.GroupByField))
        {
            if (!byKey.TryGetValue(request.GroupByField.Trim(), out groupByColumn))
            {
                throw new ArgumentException($"Group-by column '{request.GroupByField}' is not a valid column.");
            }
        }

        var searchableColumns = selected.Where(c => c.Type == "text").ToList();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        if (groupByColumn is not null)
        {
            countSql.Append(" select count(*) from ( ");
            countSql.Append($" select {Quote(groupByColumn.Name)} ");
            countSql.Append($" from {WarehouseTables.ViewInvestorPortfolioLtd} ");
        }
        else
        {
            countSql.Append($" select count(*) from {WarehouseTables.ViewInvestorPortfolioLtd} ");
        }

        await using var countCommand = new SqlCommand { Connection = connection, CommandType = System.Data.CommandType.Text };
        var countWhere = DataExplorerFilterSql.BuildWhereClause(
            searchTerm is not null ? searchableColumns : null,
            searchTerm,
            resolvedFilters,
            filterLogic,
            countCommand.Parameters);
        countSql.Append(countWhere);
        if (groupByColumn is not null)
        {
            countSql.Append($" group by {Quote(groupByColumn.Name)} ");
            countSql.Append(" ) grouped_rows ");
        }

        countCommand.CommandText = countSql.ToString();
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(BuildSelectList(selected, groupByColumn));
        pageSql.Append($" from {WarehouseTables.ViewInvestorPortfolioLtd} ");

        await using var pageCommand = new SqlCommand { Connection = connection, CommandType = System.Data.CommandType.Text };
        var pageWhere = DataExplorerFilterSql.BuildWhereClause(
            searchTerm is not null ? searchableColumns : null,
            searchTerm,
            resolvedFilters,
            filterLogic,
            pageCommand.Parameters);
        pageSql.Append(pageWhere);
        if (groupByColumn is not null)
        {
            pageSql.Append($" group by {Quote(groupByColumn.Name)} ");
        }

        pageSql.Append($" order by {Quote(sortColumn.Name)} {(descending ? "desc" : "asc")} ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        pageCommand.CommandText = pageSql.ToString();
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", pageSize);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add(reader.ToCamelCaseDictionary());
            }
        }

        _logger.LogInformation(
            "Data Explorer returned {Rows} rows for {Columns} columns (page {Page}, total {Total}).",
            rows.Count,
            selected.Count,
            page,
            totalCount);

        return new DataExplorerDataResult
        {
            Columns = selected,
            Rows = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // --- column catalog (cached) ---------------------------------------------------------------

    private async Task<IReadOnlyList<DataExplorerColumnDto>> GetColumnCatalogAsync()
    {
        if (_columnsCache is not null)
        {
            return _columnsCache;
        }

        await _columnsLock.WaitAsync();
        try
        {
            if (_columnsCache is not null)
            {
                return _columnsCache;
            }

            _columnsCache = await LoadColumnCatalogAsync();
            return _columnsCache;
        }
        finally
        {
            _columnsLock.Release();
        }
    }

    private async Task<IReadOnlyList<DataExplorerColumnDto>> LoadColumnCatalogAsync()
    {
        // Fabric uses a case-sensitive (BIN2) collation, so system views/columns must be upper-cased.
        // Alias to lower-case names so the reader lookups stay consistent with the rest of the codebase.
        const string sql =
            " select COLUMN_NAME as column_name, DATA_TYPE as data_type, ORDINAL_POSITION as ordinal_position " +
            " from INFORMATION_SCHEMA.COLUMNS " +
            " where TABLE_SCHEMA = @schema and TABLE_NAME = @table " +
            " order by ORDINAL_POSITION ";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@schema", WarehouseTables.ViewInvestorPortfolioLtdSchema);
        command.Parameters.AddWithValue("@table", WarehouseTables.ViewInvestorPortfolioLtdName);

        var columns = new List<DataExplorerColumnDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetStringOrEmpty("column_name");
            var dataType = reader.GetStringOrEmpty("data_type");
            columns.Add(new DataExplorerColumnDto
            {
                Name = name,
                Field = JsonNamingPolicy.CamelCase.ConvertName(name),
                Label = BuildLabel(name),
                Group = ClassifyGroup(name),
                DataType = dataType,
                Type = MapType(dataType),
                Ordinal = reader.GetInt32OrDefault("ordinal_position")
            });
        }

        if (columns.Count == 0)
        {
            _logger.LogWarning(
                "Data Explorer view {View} returned no columns from INFORMATION_SCHEMA.",
                WarehouseTables.ViewInvestorPortfolioLtd);
        }

        return columns;
    }

    // --- selection / validation helpers --------------------------------------------------------

    private static Dictionary<string, DataExplorerColumnDto> BuildLookup(IReadOnlyList<DataExplorerColumnDto> catalog)
    {
        var map = new Dictionary<string, DataExplorerColumnDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in catalog)
        {
            // Allow callers to reference either the raw column name or the camelCase field.
            map[column.Name] = column;
            map[column.Field] = column;
        }

        return map;
    }

    private static List<DataExplorerColumnDto> ResolveSelectedColumns(
        IEnumerable<string> requested,
        IReadOnlyDictionary<string, DataExplorerColumnDto> byKey)
    {
        var resolved = new List<DataExplorerColumnDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in requested)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (byKey.TryGetValue(key.Trim(), out var column) && seen.Add(column.Name))
            {
                resolved.Add(column);
            }
        }

        return resolved;
    }

    private static DataExplorerColumnDto ResolveSortColumn(
        string? sortBy,
        IReadOnlyDictionary<string, DataExplorerColumnDto> byKey,
        IReadOnlyList<DataExplorerColumnDto> selected)
    {
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            if (!byKey.TryGetValue(sortBy.Trim(), out var column))
            {
                throw new ArgumentException($"Sort column '{sortBy}' is not a valid column.");
            }

            return column;
        }

        // Default: keep pagination deterministic by ordering on the first selected column.
        return selected[0];
    }

    private static bool ResolveSortDirection(string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortDir))
        {
            return false;
        }

        return sortDir.Trim().ToLowerInvariant() switch
        {
            "asc" or "ascending" => false,
            "desc" or "descending" => true,
            _ => throw new ArgumentException("Query parameter 'sortDir' is invalid. Valid values: asc, desc.")
        };
    }

    private static List<ResolvedDataExplorerFilter> ResolveFilters(
        IEnumerable<DataExplorerFilterDto> filters,
        IReadOnlyDictionary<string, DataExplorerColumnDto> byKey)
    {
        var resolved = new List<ResolvedDataExplorerFilter>();
        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                continue;
            }

            if (!byKey.TryGetValue(filter.Field.Trim(), out var column))
            {
                throw new ArgumentException($"Filter field '{filter.Field}' is not a valid column.");
            }

            if (!DataExplorerFilterSql.IsAllowedOperator(filter.Operator))
            {
                throw new ArgumentException(
                    $"Filter operator '{filter.Operator}' is not supported. " +
                    "Valid values: contains, notContains, equals, notEquals, startsWith, endsWith, isEmpty, isNotEmpty.");
            }

            var op = filter.Operator.Trim();
            var needsValue = !op.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)
                && !op.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase);
            if (needsValue && string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new ArgumentException($"Filter on '{filter.Field}' requires a value.");
            }

            resolved.Add(new ResolvedDataExplorerFilter
            {
                Column = column,
                Operator = op,
                Value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim()
            });
        }

        return resolved;
    }

    private static string BuildSelectList(
        IReadOnlyList<DataExplorerColumnDto> selected,
        DataExplorerColumnDto? groupByColumn)
    {
        if (groupByColumn is null)
        {
            return string.Join(", ", selected.Select(c => Quote(c.Name)));
        }

        // When grouping, non-group columns are aggregated so SQL Server accepts the SELECT.
        var parts = new List<string>();
        foreach (var column in selected)
        {
            if (column.Name.Equals(groupByColumn.Name, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(Quote(column.Name));
                continue;
            }

            parts.Add(column.Type == "number"
                ? $"sum(isnull({Quote(column.Name)}, 0)) as {Quote(column.Name)}"
                : $"max({Quote(column.Name)}) as {Quote(column.Name)}");
        }

        return string.Join(", ", parts);
    }

    // Bracket-quote a validated identifier (defense in depth; names already come from the schema).
    private static string Quote(string name) => DataExplorerFilterSql.Quote(name);

    // --- presentation helpers ------------------------------------------------------------------

    private static string ClassifyGroup(string columnName)
    {
        var lower = columnName.ToLowerInvariant();

        if (lower.Contains("investor") || lower.Contains("relationship") || lower.Contains("contact") || lower.Contains("client"))
        {
            return DataExplorerGroups.Investors;
        }

        if (lower.Contains("fund") || lower.Contains("strategy") || lower.Contains("vintage"))
        {
            return DataExplorerGroups.Fund;
        }

        return DataExplorerGroups.Capital;
    }

    private static string MapType(string dataType) =>
        dataType.ToLowerInvariant() switch
        {
            "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney"
                or "int" or "bigint" or "smallint" or "tinyint" => "number",
            "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time" => "date",
            "bit" => "boolean",
            _ => "text"
        };

    private static string BuildLabel(string columnName)
    {
        // Split on underscores and camelCase boundaries, then Title Case (acronyms stay upper).
        var spaced = new StringBuilder();
        for (var i = 0; i < columnName.Length; i++)
        {
            var ch = columnName[i];
            if (ch == '_')
            {
                spaced.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(ch) && !char.IsUpper(columnName[i - 1]))
            {
                spaced.Append(' ');
            }

            spaced.Append(ch);
        }

        var words = spaced.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = Acronyms.Contains(words[i])
                ? words[i].ToUpperInvariant()
                : char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        }

        return string.Join(' ', words);
    }
}
