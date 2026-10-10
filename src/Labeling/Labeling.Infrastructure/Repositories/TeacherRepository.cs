using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Labeling.Domain.Teachers;
using Labeling.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Repositories;

internal sealed class TeacherRepository(LabelingDbContext db) : ITeacherRepository
{
    public Task<TeacherModel?> GetAsync(Guid id, CancellationToken ct) => db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> NameTakenAsync(string name, CancellationToken ct) => db.Teachers.AnyAsync(t => t.Name == name, ct);

    public async Task<IReadOnlyList<TeacherModel>> ListAsync(TaskType? task, bool onlyActive, CancellationToken ct) =>
        await db.Teachers
            .Where(t => task == null || t.Task == task)
            .Where(t => !onlyActive || t.IsActive)
            .OrderBy(t => t.Task).ThenBy(t => t.Name)
            .ToListAsync(ct);

    public void Add(TeacherModel teacher) => db.Teachers.Add(teacher);
}
