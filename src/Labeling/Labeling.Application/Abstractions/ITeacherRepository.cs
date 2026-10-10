using Labeling.Domain.Common;
using Labeling.Domain.Teachers;

namespace Labeling.Application.Abstractions;

public interface ITeacherRepository
{
    Task<TeacherModel?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> NameTakenAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<TeacherModel>> ListAsync(TaskType? task, bool onlyActive, CancellationToken ct);
    void Add(TeacherModel teacher);
}
