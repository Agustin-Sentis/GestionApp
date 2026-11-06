namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>One page of results plus the total number of items that match the filters.</summary>
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();

        /// <summary>Number of items that match the filters, across all pages.</summary>
        public int Total { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
    }
}
