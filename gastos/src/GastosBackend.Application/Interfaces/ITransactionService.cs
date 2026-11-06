using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<PagedResponse<TransactionResponse>> GetPage(TransactionPageRequest request, Guid userId);

        /// <summary>Every transaction that matches the filters of the request (paging is ignored), newest first, up to a limit.</summary>
        Task<List<TransactionResponse>> GetForExport(TransactionPageRequest request, Guid userId);
        Task<TransactionResponse> GetById(Guid id, Guid userId);
        Task<TransactionResponse> Create(TransactionRequest request, Guid userId);
        Task Update(TransactionRequest request, Guid id, Guid userId);
        Task Delete(Guid id, Guid userId);
    }
}
