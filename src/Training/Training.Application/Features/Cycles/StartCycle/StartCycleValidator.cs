using FluentValidation;
using Training.Domain.Cycles;

namespace Training.Application.Features.Cycles.StartCycle;

public sealed class StartCycleValidator : AbstractValidator<StartCycleCommand>
{
    public StartCycleValidator()
    {
        RuleFor(x => x.CameraId).NotEmpty();
        RuleFor(x => x.ValidationMode).IsEnumName(typeof(ValidationMode), caseSensitive: false).When(x => x.ValidationMode is not null);
        RuleFor(x => x.AgreementThreshold).InclusiveBetween(0, 1).When(x => x.AgreementThreshold is not null);
    }
}
