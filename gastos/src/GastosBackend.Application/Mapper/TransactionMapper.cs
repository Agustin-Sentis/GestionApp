using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Mapper
{
    public static class TransactionMapper
    {
        /// <remarks>Requiere la transacción con su Category cargada.</remarks>
        public static TransactionResponse ToTransactionResponse(this Transaction transaction)
        {
            return new TransactionResponse
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Date = transaction.Date,
                Note = transaction.Note,
                CategoryId = transaction.CategoryId,
                CategoryName = transaction.Category?.Name ?? string.Empty,
                Type = transaction.Category?.Type ?? default
            };
        }

        public static Transaction ToTransaction(this TransactionRequest request, Category category, Guid userId)
        {
            return new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Currency = request.Currency,
                Date = request.Date,
                Note = NormalizeNote(request.Note),
                Category = category,
                CategoryId = category.Id,
                UserId = userId
            };
        }

        public static Transaction ToTransactionUpdate(this Transaction entity, TransactionRequest request)
        {
            entity.Amount = request.Amount;
            entity.Currency = request.Currency;
            entity.Date = request.Date;
            entity.Note = NormalizeNote(request.Note);
            entity.CategoryId = request.CategoryId;
            return entity;
        }

        private static string? NormalizeNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }
    }
}
