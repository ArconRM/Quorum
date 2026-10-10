using FluentValidation;
using Labeling.Domain.Common;
using Labeling.Domain.Teachers;

namespace Labeling.Application.Features.Teachers.RegisterTeacher;

public sealed class RegisterTeacherValidator : AbstractValidator<RegisterTeacherCommand>
{
    public RegisterTeacherValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).IsEnumName(typeof(TeacherKind), caseSensitive: false);
        RuleFor(x => x.Task).IsEnumName(typeof(TaskType), caseSensitive: false);
        RuleFor(x => x.Weight).GreaterThan(0).LessThanOrEqualTo(TeacherModel.MaxWeight);
    }
}
