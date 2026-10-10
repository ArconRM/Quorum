using Labeling.Domain.Teachers;

namespace Labeling.Domain.Runs;

/// <summary>Учитель в составе прогона — снимок его веса на момент старта и прогресс.</summary>
public sealed class RunTeacher
{
    private RunTeacher() { }

    internal RunTeacher(TeacherModel teacher)
    {
        TeacherId = teacher.Id;
        Name = teacher.Name;
        Kind = teacher.Kind;
        Weight = teacher.Weight;
    }

    public Guid TeacherId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TeacherKind Kind { get; private set; }
    public double Weight { get; private set; }
    public int ProcessedFrames { get; internal set; }
    public bool IsCompleted { get; internal set; }
}
