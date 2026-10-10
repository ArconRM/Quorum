using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Labeling.Domain.Teachers;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Domain;

namespace Labeling.Application.Features.Teachers.RegisterTeacher;

public sealed class RegisterTeacherHandler(ITeacherRepository teachers, IUnitOfWork uow) : IRequestHandler<RegisterTeacherCommand, Guid>
{
    public async Task<Guid> Handle(RegisterTeacherCommand cmd, CancellationToken ct)
    {
        if (await teachers.NameTakenAsync(cmd.Name.Trim(), ct)) throw new DomainException($"Teacher '{cmd.Name}' already exists");
        var teacher = TeacherModel.Register(cmd.Name,
            Enum.Parse<TeacherKind>(cmd.Kind, ignoreCase: true), Enum.Parse<TaskType>(cmd.Task, ignoreCase: true), cmd.Weight);
        teachers.Add(teacher);
        await uow.CommitAsync(ct);
        return teacher.Id;
    }
}
