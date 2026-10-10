using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Teachers.UpdateTeacher;

public sealed class UpdateTeacherHandler(ITeacherRepository teachers, IUnitOfWork uow) : IRequestHandler<UpdateTeacherCommand, TeacherDto>
{
    public async Task<TeacherDto> Handle(UpdateTeacherCommand cmd, CancellationToken ct)
    {
        var teacher = await teachers.GetAsync(cmd.TeacherId, ct) ?? throw new NotFoundException($"Teacher {cmd.TeacherId} not found");
        if (cmd.Weight is { } weight) teacher.ChangeWeight(weight);
        if (cmd.IsActive == true) teacher.Activate();
        if (cmd.IsActive == false) teacher.Deactivate();
        await uow.CommitAsync(ct);
        return TeacherDto.From(teacher);
    }
}
