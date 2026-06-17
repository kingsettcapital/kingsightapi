namespace kingsightapi.Entities;

using System.Text.Json.Serialization;
using kingsightapi.Configuration;

/// <summary>
/// Data element groups shown in the Data Explorer left panel.
/// Order here drives the display order of the groups.
/// </summary>
public static class DataExplorerGroups
{
    public const string Investors = "Investors";
    public const string Fund = "Fund";
    public const string Capital = "Capital";
}

/// <summary>A single selectable column from the explorer view, with display metadata.</summary>
public sealed class DataExplorerColumnDto
{
    /// <summary>Raw database column name (used server-side only; never built from user input).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>camelCase key used for the column inside each data row object.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Human-friendly label for the UI (e.g. "Investor Name").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Data element group: Investors, Fund, or Capital.</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>Raw SQL data type from INFORMATION_SCHEMA (e.g. "decimal", "nvarchar").</summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>Simplified type for the frontend: number, text, date, or boolean.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Column position in the view.</summary>
    public int Ordinal { get; init; }
}

/// <summary>One group of columns for the left-side selector.</summary>
public sealed class DataExplorerColumnGroupDto
{
    public string Group { get; init; } = string.Empty;
    public IReadOnlyList<DataExplorerColumnDto> Columns { get; init; } = [];
}

/// <summary>One filter row from the Filters panel (column + condition + value).</summary>
public sealed class DataExplorerFilterDto
{
    public string Field { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public string? Value { get; init; }
}

/// <summary>Request body for the data population endpoint (selected columns + filters + paging/sort).</summary>
public sealed class DataExplorerDataRequest
{
    /// <summary>Columns the user selected. Accepts raw column names or camelCase field names.</summary>
    public List<string> Columns { get; init; } = [];

    public List<DataExplorerFilterDto> Filters { get; init; } = [];

    /// <summary>How filter rows combine: and or or.</summary>
    public string? FilterLogic { get; init; }

    /// <summary>Optional group-by column (raw name or camelCase field).</summary>
    public string? GroupByField { get; init; }

    /// <summary>Optional free-text filter applied across the selected text columns.</summary>
    public string? Search { get; init; }

    /// <summary>Column to sort by (defaults to the first selected column).</summary>
    public string? SortBy { get; init; }

    /// <summary>Sort direction: asc or desc.</summary>
    public string? SortDir { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>Body for creating or updating a saved explorer template.</summary>
public sealed class DataExplorerSaveTemplateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Columns { get; init; } = [];
    public List<DataExplorerFilterDto> Filters { get; init; } = [];
    public string? FilterLogic { get; init; }
    public string? GroupByField { get; init; }
}

/// <summary>Saved template returned to the frontend (same shape as the save body plus metadata).</summary>
public sealed class DataExplorerTemplateDto
{
    /// <summary>Fabric BIGINT IDENTITY — serialized as string in JSON for JavaScript precision.</summary>
    [JsonConverter(typeof(LongAsStringJsonConverter))]
    public long TemplateId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SourceView { get; init; } = string.Empty;
    public List<string> Columns { get; init; } = [];
    public List<DataExplorerFilterDto> Filters { get; init; } = [];
    public string FilterLogic { get; init; } = "and";
    public string? GroupByField { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ModifiedBy { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>Template row for the Saved list panel.</summary>
public sealed class DataExplorerTemplateSummaryDto
{
    /// <summary>Fabric BIGINT IDENTITY — serialized as string in JSON for JavaScript precision.</summary>
    [JsonConverter(typeof(LongAsStringJsonConverter))]
    public long TemplateId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int ColumnCount { get; init; }
    public int FilterCount { get; init; }
    public string? GroupByField { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// Paginated explorer data. Columns echoes the resolved selection (in order); each row is a
/// camelCase-keyed object so the frontend grid and export can bind dynamically.
/// </summary>
public sealed class DataExplorerDataResult
{
    public IReadOnlyList<DataExplorerColumnDto> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
