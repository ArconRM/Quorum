using Quorum.Domain;

namespace Training.Domain.Cycles;

/// <summary>Value object: параметры проверки разметки для цикла.</summary>
public sealed class ValidationSettings
{
    private ValidationSettings() { }

    public ValidationMode Mode { get; private set; }
    public double AgreementThreshold { get; private set; }

    public static ValidationSettings Create(ValidationMode mode, double agreementThreshold)
    {
        if (agreementThreshold is < 0 or > 1) throw new DomainException("Agreement threshold must be in [0, 1]");
        return new ValidationSettings { Mode = mode, AgreementThreshold = agreementThreshold };
    }
}
