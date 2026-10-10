using Labeling.Domain.Common;
using Quorum.Domain;

namespace Labeling.Domain.Teachers;

/// <summary>Корень агрегата: модель-учитель в реестре ансамбля.</summary>
public sealed class TeacherModel : Entity
{
    public const double MaxWeight = 10;

    private TeacherModel() { }

    public string Name { get; private set; } = string.Empty;
    public TeacherKind Kind { get; private set; }
    public TaskType Task { get; private set; }
    public double Weight { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static TeacherModel Register(string name, TeacherKind kind, TaskType task, double weight)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Teacher name is required");
        EnsureKindFitsTask(kind, task);
        var teacher = new TeacherModel
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Kind = kind,
            Task = task,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        teacher.ChangeWeight(weight);
        return teacher;
    }

    public void ChangeWeight(double weight)
    {
        if (weight <= 0 || weight > MaxWeight) throw new DomainException($"Weight must be in (0, {MaxWeight}]");
        Weight = weight;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static void EnsureKindFitsTask(TeacherKind kind, TaskType task)
    {
        var ok = task switch
        {
            TaskType.Detection => kind is TeacherKind.OpenVocabDetector or TeacherKind.Detector,
            TaskType.Ocr => kind is TeacherKind.Vlm,
            _ => false,
        };
        if (!ok) throw new DomainException($"{kind} teacher cannot be used for {task}");
    }
}
