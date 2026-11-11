using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class CategoryBudgetRequestValidations : AbstractValidator<CategoryBudgetRequest>
    {
        public CategoryBudgetRequestValidations()
        {
            RuleFor(r => r.MonthlyBudget)
                .GreaterThan(0).WithMessage("El presupuesto debe ser mayor a cero.")
                .LessThanOrEqualTo(1_000_000_000).WithMessage("El presupuesto no puede superar los 1.000.000.000.")
                .When(r => r.MonthlyBudget != null);
        }
    }
}
