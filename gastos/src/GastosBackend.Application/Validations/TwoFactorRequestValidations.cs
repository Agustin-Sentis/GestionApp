using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    internal static class TwoFactorRules
    {
        public static IRuleBuilderOptions<T, string> PasswordRule<T>(this IRuleBuilder<T, string> rule) => rule
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(100).WithMessage("La contraseña no puede superar los 100 caracteres.");

        // A code from the app has 6 digits and a recovery one 11 characters; the rest is room for spaces
        public static IRuleBuilderOptions<T, string> CodeRule<T>(this IRuleBuilder<T, string> rule) => rule
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(20).WithMessage("El código no es válido.");
    }

    public class TwoFactorSetupRequestValidations : AbstractValidator<TwoFactorSetupRequest>
    {
        public TwoFactorSetupRequestValidations() => RuleFor(r => r.Password).PasswordRule();
    }

    public class VerifyTwoFactorRequestValidations : AbstractValidator<VerifyTwoFactorRequest>
    {
        public VerifyTwoFactorRequestValidations()
        {
            RuleFor(r => r.ChallengeToken)
                .NotEmpty().WithMessage("Falta la verificación de la contraseña. Ingresá de nuevo.")
                .MaximumLength(2000).WithMessage("Tu verificación venció, ingresá de nuevo.");
            RuleFor(r => r.Code).CodeRule();
        }
    }

    public class TwoFactorEnableRequestValidations : AbstractValidator<TwoFactorEnableRequest>
    {
        public TwoFactorEnableRequestValidations() => RuleFor(r => r.Code).CodeRule();
    }

    public class TwoFactorPasswordCodeRequestValidations : AbstractValidator<TwoFactorPasswordCodeRequest>
    {
        public TwoFactorPasswordCodeRequestValidations()
        {
            RuleFor(r => r.Password).PasswordRule();
            RuleFor(r => r.Code).CodeRule();
        }
    }
}
