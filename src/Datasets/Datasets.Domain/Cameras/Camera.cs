using Quorum.Domain;

namespace Datasets.Domain.Cameras;

/// <summary>
/// Корень агрегата: источник кадров (камера) и правила её цикла адаптации.
/// Цикл запускается, когда прошло CycleIntervalHours с прошлого запроса И накопилось MinFramesPerCycle новых кадров.
/// </summary>
public sealed class Camera : Entity
{
    private Camera() { }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TaskType Task { get; private set; }

    /// <summary>Текстовый запрос для учителей: «license plate» для open-vocab детекторов, инструкция для VLM.</summary>
    public string Prompt { get; private set; } = string.Empty;

    public int MinFramesPerCycle { get; private set; }
    public int CycleIntervalHours { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastCycleRequestedAt { get; private set; }

    public static Camera Create(string name, string description, TaskType task, string prompt, int minFramesPerCycle, int cycleIntervalHours)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Camera name is required");
        var camera = new Camera
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Task = task,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        camera.ChangePrompt(prompt);
        camera.ChangeSchedule(minFramesPerCycle, cycleIntervalHours);
        return camera;
    }

    public void ChangePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new DomainException("Prompt is required");
        Prompt = prompt.Trim();
    }

    public void ChangeSchedule(int minFramesPerCycle, int cycleIntervalHours)
    {
        if (minFramesPerCycle < 1) throw new DomainException("Minimum frames per cycle must be at least 1");
        if (cycleIntervalHours < 1) throw new DomainException("Cycle interval must be at least 1 hour");
        MinFramesPerCycle = minFramesPerCycle;
        CycleIntervalHours = cycleIntervalHours;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Подошёл ли срок по расписанию (без учёта числа кадров).</summary>
    public bool IsCycleDue(DateTime now) =>
        IsActive && (LastCycleRequestedAt is null || now - LastCycleRequestedAt.Value >= TimeSpan.FromHours(CycleIntervalHours));

    public bool HasEnoughFrames(int newFrames) => newFrames >= MinFramesPerCycle;

    public void MarkCycleRequested(DateTime now)
    {
        if (!IsActive) throw new DomainException("Inactive camera cannot request a cycle");
        LastCycleRequestedAt = now;
    }

    public void EnsureCanAcceptFrames()
    {
        if (!IsActive) throw new DomainException($"Camera '{Name}' is inactive");
    }
}
