using Labeling.Domain.Labels;
using Quorum.Domain;

namespace Labeling.Domain.Runs;

/// <summary>Value object: стратегия валидации псевдоразметки — главная развилка исследования.</summary>
public sealed class ValidationStrategy
{
    private ValidationStrategy() { }

    public ValidationMode Mode { get; private set; }
    public double AgreementThreshold { get; private set; }

    public static ValidationStrategy Create(ValidationMode mode, double agreementThreshold)
    {
        if (agreementThreshold is < 0 or > 1) throw new DomainException("Agreement threshold must be in [0, 1]");
        return new ValidationStrategy { Mode = mode, AgreementThreshold = agreementThreshold };
    }

    public LabelStatus Decide(double agreement) => Mode switch
    {
        ValidationMode.Manual => LabelStatus.NeedsReview,
        ValidationMode.Auto => agreement >= AgreementThreshold ? LabelStatus.Accepted : LabelStatus.Rejected,
        ValidationMode.Hybrid => agreement >= AgreementThreshold ? LabelStatus.Accepted : LabelStatus.NeedsReview,
        _ => throw new DomainException($"Unknown validation mode {Mode}"),
    };
}
