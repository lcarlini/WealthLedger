namespace WealthLedger.Contracts.Domain.Pagination;

public class PagedResponse<T>
{
    public int Page { get; }
    public int PageSize { get; }
    public long TotalCount { get; }
    public IEnumerable<T> Items { get; }

    public PagedResponse(int page, int pageSize, long totalCount, IEnumerable<T> items)
    {
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        Items = items;
    }
}
