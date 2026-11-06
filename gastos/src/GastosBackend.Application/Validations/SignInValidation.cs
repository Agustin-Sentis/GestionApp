using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    /// <summary>
    /// Only checks that the credentials are present and have a sane size (so a huge password is never hashed).
    /// The password rules are not applied here: they belong to sign up, and users created earlier must still be able to sign in.
    /// </summary>
    public class SignInValidator : AbstractValidator<SignInRequest>
    {
        public SignInValidator()
        {
            RuleFor(sr => sr.Email)
                .NotEmpty().WithMessage("El email es obligatorio.")
                .MaximumLength(254).WithMessage("El email no puede superar los 254 caracteres.");
            RuleFor(sr => sr.Password)
                .NotEmpty().WithMessage("La contraseña es obligatoria.")
                .MaximumLength(100).WithMessage("La contraseña no puede superar los 100 caracteres.");
        }
    }
}
