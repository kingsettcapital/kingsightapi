using kingsightapi.Entities;

namespace kingsightapi.Services;

/// <summary>
/// Backs the Data Explorer tool over <c>view_investor_portfolio_ltd</c>:
/// one call lists selectable columns (grouped + typed), the other returns data for the selection.
/// </summary>
public interface IDataExplorerService
{
    /// <summary>Explorer product dropdown (Investor Data Product / Asset Data Product).</summary>
    Task<IReadOnlyList<PortalFilterOptionDto>> GetProductsAsync();

    /// <summary>Columns available in the explorer view, grouped by product.</summary>
    Task<IReadOnlyList<DataExplorerColumnGroupDto>> GetColumnsAsync(string? product);

    /// <summary>Paginated rows for the selected columns, filters, and optional group-by.</summary>
    Task<DataExplorerDataResult> GetDataAsync(DataExplorerDataRequest request);

    /// <summary>Saves a new explorer template (name, columns, filters, group-by).</summary>
    Task<DataExplorerTemplateDto> SaveTemplateAsync(DataExplorerSaveTemplateRequest request, string? createdBy);

    /// <summary>Updates an existing template (replaces columns and filters).</summary>
    Task<DataExplorerTemplateDto> UpdateTemplateAsync(long templateId, DataExplorerSaveTemplateRequest request, string? modifiedBy);

    /// <summary>Active saved templates for the Saved panel, optionally filtered by product.</summary>
    Task<IReadOnlyList<DataExplorerTemplateSummaryDto>> GetTemplatesAsync(string? product);

    /// <summary>Loads one template by id (for reopening in the explorer).</summary>
    Task<DataExplorerTemplateDto?> GetTemplateAsync(long templateId);

    /// <summary>Soft-deletes a template (is_active = 0).</summary>
    Task DeleteTemplateAsync(long templateId, string? modifiedBy);
}
