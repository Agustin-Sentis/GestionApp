namespace GastosBackend.Application.dtos.Requests
{
    public class TransactionFilterRequest
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public Guid? CategoryId { get; set; }
    }
}
