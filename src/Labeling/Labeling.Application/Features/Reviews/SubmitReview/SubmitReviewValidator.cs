using FluentValidation;
using Labeling.Domain.Labels;

namespace Labeling.Application.Features.Reviews.SubmitReview;

public sealed class SubmitReviewValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewValidator()
    {
        RuleFor(x => x.Verdict).IsEnumName(typeof(ReviewVerdict), caseSensitive: false);
        RuleFor(x => x).Must(x => x.Boxes is not null || !string.IsNullOrWhiteSpace(x.Text))
            .When(x => string.Equals(x.Verdict, nameof(ReviewVerdict.Correct), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Correct verdict requires boxes (detection) or text (OCR)");
        RuleForEach(x => x.Boxes).ChildRules(box =>
        {
            box.RuleFor(b => b.W).GreaterThan(0);
            box.RuleFor(b => b.H).GreaterThan(0);
            box.RuleFor(b => b.X).InclusiveBetween(0, 1);
            box.RuleFor(b => b.Y).InclusiveBetween(0, 1);
        });
    }
}
