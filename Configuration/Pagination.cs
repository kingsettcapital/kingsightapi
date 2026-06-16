namespace kingsightapi.Configuration;

public static class Pagination
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize, int Offset) Normalize(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? DefaultPage : page;
        var normalizedPageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var offset = (normalizedPage - 1) * normalizedPageSize;
        return (normalizedPage, normalizedPageSize, offset);
    }
}
