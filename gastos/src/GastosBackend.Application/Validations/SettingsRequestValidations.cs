using FluentValidation;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Validations
{
    public class SettingsRequestValidations : AbstractValidator<SettingsRequest>
    {
        public SettingsRequestValidations()
        {
            // A name that is not in the enum is rejected when the body is read; this catches a number out of range (for example 7)
            RuleFor(sr => sr.DollarType)
                .IsInEnum().WithMessage("El tipo de dólar no es válido. Usá Official, Blue o Mep.");
        }
    }
}
