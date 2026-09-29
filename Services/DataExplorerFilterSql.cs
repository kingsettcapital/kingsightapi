using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Builds parameterized WHERE fragments for Data Explorer filter rows.</summary>
internal static class DataExplorerFilterSql
{
  private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
  {
    "contains",
    "notContains",
    "equals",
    "notEquals",
    "startsWith",
    "endsWith",
    "isEmpty",
    "isNotEmpty"
  };

  public static bool IsAllowedOperator(string? op) =>
    !string.IsNullOrWhiteSpace(op) && AllowedOperators.Contains(op.Trim());

  /// <summary>AND/OR for SQL WHERE — defaults to AND when empty (data query only).</summary>
  public static string NormalizeFilterLogic(string? filterLogic)
  {
    if (string.IsNullOrWhiteSpace(filterLogic))
    {
      return "AND";
    }

    return filterLogic.Trim().Equals("or", StringComparison.OrdinalIgnoreCase) ? "OR" : "AND";
  }

  /// <summary>Persist filter logic as-is; empty string is stored when the client sends none.</summary>
  public static string NormalizeMatchTypeForStorage(string? filterLogic)
  {
    if (string.IsNullOrWhiteSpace(filterLogic))
    {
      return string.Empty;
    }

    return filterLogic.Trim().Equals("or", StringComparison.OrdinalIgnoreCase) ? "OR" : "AND";
  }

  /// <summary>Maps stored match_type back to the frontend filterLogic value.</summary>
  public static string? MapFilterLogicFromStorage(string? matchType)
  {
    if (string.IsNullOrWhiteSpace(matchType))
    {
      return string.Empty;
    }

    return matchType.Equals("OR", StringComparison.OrdinalIgnoreCase) ? "or" : "and";
  }

  /// <summary>
  /// Appends a WHERE clause combining optional quick-search and structured filters.
  /// Filter values are bound as @filter0, @filter1, ...
  /// </summary>
  public static string BuildWhereClause(
    IReadOnlyList<DataExplorerColumnDto>? searchableColumns,
    string? searchTerm,
    IReadOnlyList<ResolvedDataExplorerFilter> filters,
    string filterLogic,
    SqlParameterCollection parameters,
    string? columnAlias = null)
  {
    var conditions = new List<string>();

    if (searchTerm is not null && searchableColumns is { Count: > 0 })
    {
      var searchParts = new StringBuilder(" ( ");
      searchParts.Append(" lower(isnull(cast(");
      searchParts.Append(QualifyColumn(columnAlias, searchableColumns[0].Name));
      searchParts.Append(" as nvarchar(4000)), '')) like '%' + lower(@search) + '%' ");
      for (var i = 1; i < searchableColumns.Count; i++)
      {
        searchParts.Append(" or lower(isnull(cast(");
        searchParts.Append(QualifyColumn(columnAlias, searchableColumns[i].Name));
        searchParts.Append(" as nvarchar(4000)), '')) like '%' + lower(@search) + '%' ");
      }

      searchParts.Append(" ) ");
      conditions.Add(searchParts.ToString());
      parameters.AddWithValue("@search", searchTerm);
    }

    for (var i = 0; i < filters.Count; i++)
    {
      var filter = filters[i];
      var paramName = $"@filter{i}";
      conditions.Add(BuildFilterCondition(filter.Column.Name, filter.Operator, paramName, columnAlias));
      parameters.AddWithValue(paramName, (object?)filter.Value ?? DBNull.Value);
    }

    if (conditions.Count == 0)
    {
      return string.Empty;
    }

    var joiner = $" {filterLogic} ";
    return " where " + string.Join(joiner, conditions);
  }

  private static string BuildFilterCondition(string columnName, string op, string paramName, string? columnAlias)
  {
    var quoted = QualifyColumn(columnAlias, columnName);
    var col = $"isnull(cast({quoted} as nvarchar(4000)), '')";
    var lowerCol = $"lower({col})";

    return op.ToLowerInvariant() switch
    {
      "contains" => $" {lowerCol} like '%' + lower({paramName}) + '%' ",
      "notcontains" => $" {lowerCol} not like '%' + lower({paramName}) + '%' ",
      "equals" => $" {lowerCol} = lower({paramName}) ",
      "notequals" => $" {lowerCol} <> lower({paramName}) ",
      "startswith" => $" {lowerCol} like lower({paramName}) + '%' ",
      "endswith" => $" {lowerCol} like '%' + lower({paramName}) ",
      "isempty" => $" ({quoted} is null or {col} = '') ",
      "isnotempty" => $" ({quoted} is not null and {col} <> '') ",
      _ => throw new ArgumentException($"Filter operator '{op}' is not supported.")
    };
  }

  private static string QualifyColumn(string? columnAlias, string columnName) =>
    string.IsNullOrWhiteSpace(columnAlias) ? Quote(columnName) : $"{columnAlias}.{Quote(columnName)}";

  public static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
}

internal sealed class ResolvedDataExplorerFilter
{
  public required DataExplorerColumnDto Column { get; init; }
  public required string Operator { get; init; }
  public string? Value { get; init; }
}
