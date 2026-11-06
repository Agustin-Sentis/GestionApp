using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Requests
{
    /// <summary>
    /// Query of the paged transaction list. All filters are optional.
    /// Use either Year + Month or From + To to restrict the dates, not both.
    /// </summary>
    public class TransactionPageRequest
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
        public const int MaxSearchLength = 100;

        public int? Year { get; set; }
        public int? Month { get; set; }

        /// <summary>First day to include.</summary>
        public DateOnly? From { get; set; }

        /// <summary>Last day to include.</summary>
        public DateOnly? To { get; set; }

        public Guid? CategoryId { get; set; }

        /// <summary>Expense or Income, taken from the transaction's category.</summary>
        public TransactionType? Type { get; set; }

        /// <summary>Text to look for inside the note (any part of it, ignoring case). Blank means no filter.</summary>
        public string? Search { get; set; }

        /// <summary>1-based page number.</summary>
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = DefaultPageSize;
    }
}
