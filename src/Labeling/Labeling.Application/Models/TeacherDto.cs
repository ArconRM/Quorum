using Labeling.Domain.Teachers;

namespace Labeling.Application.Models;

public sealed record TeacherDto(Guid Id, string Name, string Kind, string Task, double Weight, bool IsActive)
{
    public static TeacherDto From(TeacherModel t) => new(t.Id, t.Name, t.Kind.ToString(), t.Task.ToString(), t.Weight, t.IsActive);
}
