using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class ChangeEmailRequestValidations : AbstractValidator<ChangeEmailRequest>
    {
        public ChangeEmailRequestValidations()
        {
            RuleFor(r => r.Email)
                .NotEmpty().WithMessage("El email es obligatorio.")
                .MaximumLength(254).WithMessage("El email no puede superar los 254 caracteres.")
                .EmailAddress().WithMessage("El formato del email no es válido.");
            // Only that there is one: whether it is the right one is the business of the service (the strength rules are for new passwords)
            RuleFor(r => r.Password)
                .NotEmpty().WithMessage("La contraseña es obligatoria.")
                .MaximumLength(100).WithMessage("La contraseña no puede superar los 100 caracteres.");
        }
    }
}
