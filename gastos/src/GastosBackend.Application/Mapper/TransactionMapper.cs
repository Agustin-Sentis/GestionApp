using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Mapper
{
    public static class TransactionMapper
    {
        /// <summary>The name of a dollar type as it is written to the user: "oficial", "blue", "MEP".</summary>
        public static string Label(this DollarType type) => type switch
        {
            DollarType.Blue => "blue",
            DollarType.Mep => "MEP",
            _ => "oficial"
        };

        /// <summary>The rate this transaction stored for the given dollar type, or null if it has none.</summary>
        public static decimal? RateOf(this Transaction transaction, DollarType type) => type switch
        {
            DollarType.Blue => transaction.BlueExchangeRate,
            DollarType.Mep => transaction.MepExchangeRate,
            _ => transaction.ExchangeRate
        };

        /// <summary>Stores the three rates on the transaction (null for the ones that could not be obtained).</summary>
        public static void ApplyRates(this Transaction transaction, UsdRates rates)
        {
            transaction.ExchangeRate = rates.Official;
            transaction.BlueExchangeRate = rates.Blue;
            transaction.MepExchangeRate = rates.Mep;
        }

        /// <remarks>
        /// Requiere la transacción con su Category cargada. La cotización y el monto en pesos son los del dólar con el que se
        /// guardó el movimiento, no los del que el usuario tenga elegido ahora.
        /// </remarks>
        public static TransactionResponse ToTransactionResponse(this Transaction transaction)
        {
            return new TransactionResponse
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                ExchangeRate = transaction.RateOf(transaction.DollarType),
                ArsAmount = ToArs(transaction),
                DollarType = transaction.DollarType,
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

        private static decimal? ToArs(Transaction transaction)
        {
            if (transaction.Currency == Currency.ARS)
            {
                return transaction.Amount;
            }

            return transaction.RateOf(transaction.DollarType) is { } rate
                ? Math.Round(transaction.Amount * rate, 2)
                : null;
        }

        private static string? NormalizeNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }
    }
}
