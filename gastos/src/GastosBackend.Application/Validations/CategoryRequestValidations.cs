using FluentValidation;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Validations
{
    public class CategoryRequestValidations : AbstractValidator<CategoryRequest>
    {
        public CategoryRequestValidations()
        {
            RuleFor(cr => cr.Name)
                .NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
                .MaximumLength(60).WithMessage("El nombre de la categoría no puede superar los 60 caracteres.");
            RuleFor(cr => cr.Type)
                .IsInEnum<CategoryRequest, TransactionType>()
                .WithMessage("El tipo de categoría no es válido. Usá Expense o Income.");
        }
    }
}
