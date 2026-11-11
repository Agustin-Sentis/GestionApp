using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class SignUpValidator : AbstractValidator<SignUpRequest>
    {
        public SignUpValidator()
        {
            RuleFor(ur => ur.Email)
                .NotEmpty().WithMessage("El email es obligatorio.")
                .MaximumLength(254).WithMessage("El email no puede superar los 254 caracteres.")
                .EmailAddress().WithMessage("El formato del email no es válido.");
            RuleFor(ur => ur.Password).MeetsPasswordRules();
        }
    }
}
