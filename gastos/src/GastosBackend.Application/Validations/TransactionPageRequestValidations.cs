using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class TransactionPageRequestValidations : AbstractValidator<TransactionPageRequest>
    {
        public TransactionPageRequestValidations()
        {
            RuleFor(pr => pr.Page)
                .GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
            RuleFor(pr => pr.PageSize)
                .InclusiveBetween(1, TransactionPageRequest.MaxPageSize)
                .WithMessage($"El tamaño de página debe estar entre 1 y {TransactionPageRequest.MaxPageSize}.");

            // Year and month only make sense together
            RuleFor(pr => pr)
                .Must(pr => pr.Year.HasValue == pr.Month.HasValue)
                .WithMessage("El año y el mes deben indicarse juntos.")
                .OverridePropertyName(nameof(TransactionPageRequest.Month));
            // The nullable overloads accept null, so these only apply when the value is present
            RuleFor(pr => pr.Month)
                .InclusiveBetween(1, 12).WithMessage("El mes debe estar entre 1 y 12.");
            RuleFor(pr => pr.Year)
                .InclusiveBetween(2000, 2100).WithMessage("El año debe estar entre 2000 y 2100.");

            // A month and a free date range are two ways of saying the same thing: accept only one
            RuleFor(pr => pr)
                .Must(pr => !((pr.Year.HasValue || pr.Month.HasValue) && (pr.From.HasValue || pr.To.HasValue)))
                .WithMessage("Usá año y mes, o un rango de fechas (desde y hasta), pero no ambos.")
                .OverridePropertyName(nameof(TransactionPageRequest.From));
            RuleFor(pr => pr)
                .Must(pr => pr.From!.Value <= pr.To!.Value)
                .WithMessage("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.")
                .OverridePropertyName(nameof(TransactionPageRequest.From))
                .When(pr => pr.From.HasValue && pr.To.HasValue);

            RuleFor(pr => pr.Type)
                .IsInEnum().WithMessage("El tipo no es válido. Usá Expense o Income.");

            RuleFor(pr => pr.Search)
                .MaximumLength(TransactionPageRequest.MaxSearchLength)
                .WithMessage($"El texto a buscar no puede superar los {TransactionPageRequest.MaxSearchLength} caracteres.");
        }
    }
}
