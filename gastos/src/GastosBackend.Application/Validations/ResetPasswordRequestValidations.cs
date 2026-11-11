using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class ResetPasswordRequestValidations : AbstractValidator<ResetPasswordRequest>
    {
        public ResetPasswordRequestValidations()
        {
            RuleFor(r => r.NewPassword).MeetsPasswordRules();
        }
    }
}
