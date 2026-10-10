using Datasets.Domain.Frames;
using FluentValidation;

namespace Datasets.Application.Features.Frames.UploadFrame;

public sealed class UploadFrameValidator : AbstractValidator<UploadFrameCommand>
{
    public const long MaxSizeBytes = 20 * 1024 * 1024;

    public UploadFrameValidator()
    {
        RuleFor(x => x.ContentType).Must(Frame.AllowedContentTypes.Contains)
            .WithMessage($"Content type must be one of: {string.Join(", ", Frame.AllowedContentTypes)}");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, MaxSizeBytes);
    }
}
