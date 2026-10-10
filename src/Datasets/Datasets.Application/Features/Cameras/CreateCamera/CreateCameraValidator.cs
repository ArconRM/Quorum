using Datasets.Domain.Cameras;
using FluentValidation;

namespace Datasets.Application.Features.Cameras.CreateCamera;

public sealed class CreateCameraValidator : AbstractValidator<CreateCameraCommand>
{
    public CreateCameraValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Task).IsEnumName(typeof(TaskType), caseSensitive: false)
            .WithMessage("Task must be Detection or Ocr");
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.MinFramesPerCycle).InclusiveBetween(1, 100_000);
        RuleFor(x => x.CycleIntervalHours).InclusiveBetween(1, 24 * 365);
    }
}
