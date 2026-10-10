using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using Labeling.Domain.Common;
using MediatR;

namespace Labeling.Application.Features.Teachers.ListTeachers;

public sealed class ListTeachersHandler(ITeacherRepository teachers) : IRequestHandler<ListTeachersQuery, IReadOnlyList<TeacherDto>>
{
    public async Task<IReadOnlyList<TeacherDto>> Handle(ListTeachersQuery q, CancellationToken ct)
    {
        TaskType? task = Enum.TryParse<TaskType>(q.Task, ignoreCase: true, out var t) ? t : null;
        return (await teachers.ListAsync(task, onlyActive: false, ct)).Select(TeacherDto.From).ToList();
    }
}
