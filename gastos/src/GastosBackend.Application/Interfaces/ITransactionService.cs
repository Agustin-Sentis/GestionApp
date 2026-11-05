using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<List<TransactionResponse>> GetAll(TransactionFilterRequest filter, string sub);
        Task<TransactionResponse> Create(TransactionRequest request, string sub);
        Task Update(TransactionRequest request, Guid id, string sub);
        Task Delete(Guid id, string sub);
    }
}
