using FluentValidation;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Validations
{
    public class TransactionRequestValidations : AbstractValidator<TransactionRequest>
    {
        public TransactionRequestValidations()
        {
            RuleFor(tr => tr.Amount)
                .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.")
                .LessThanOrEqualTo(1_000_000_000).WithMessage("El monto no puede superar los 1.000.000.000.");
            RuleFor(tr => tr.Currency)
                .IsInEnum<TransactionRequest, Currency>()
                .WithMessage("La moneda no es válida. Usá ARS o USD.");
            RuleFor(tr => tr.Date)
                .Must(date => date != default).WithMessage("La fecha es obligatoria.");
            RuleFor(tr => tr.Note)
                .MaximumLength(200).WithMessage("La nota no puede superar los 200 caracteres.")
                .When(tr => tr.Note != null);
            RuleFor(tr => tr.CategoryId)
                .NotEmpty().WithMessage("La categoría es obligatoria.");
        }
    }
}
