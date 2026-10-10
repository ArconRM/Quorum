using Labeling.Domain.Common;
using Labeling.Domain.Labels;
using Labeling.Domain.Teachers;
using Quorum.Domain;

namespace Labeling.Domain.Runs;

/// <summary>
/// Корень агрегата: прогон ансамбля учителей по снимку датасета в рамках одного цикла адаптации.
/// Running → (консенсус) → Reviewing → Completed, либо сразу Completed, если проверять нечего.
/// </summary>
public sealed class LabelingRun : Entity
{
    private List<RunTeacher> _teachers = [];

    private LabelingRun() { }

    public Guid CycleId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public TaskType Task { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public ValidationStrategy Strategy { get; private set; } = null!;
    public RunStatus Status { get; private set; }
    public int TotalFrames { get; private set; }
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    public int PendingReview { get; private set; }
    public int Reviewed { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<RunTeacher> Teachers => _teachers;

    public static LabelingRun Start(Guid cycleId, Guid snapshotId, TaskType task, string prompt,
        ValidationStrategy strategy, IReadOnlyCollection<TeacherModel> teachers)
    {
        if (cycleId == Guid.Empty || snapshotId == Guid.Empty) throw new DomainException("Cycle and snapshot are required");
        if (teachers.Count == 0) throw new DomainException($"No active teachers for task {task}");
        if (teachers.Any(t => t.Task != task || !t.IsActive))
            throw new DomainException("All teachers must be active and match the run task");

        return new LabelingRun
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            SnapshotId = snapshotId,
            Task = task,
            Prompt = prompt,
            Strategy = strategy,
            Status = RunStatus.Running,
            CreatedAt = DateTime.UtcNow,
            _teachers = teachers.Select(t => new RunTeacher(t)).ToList(),
        };
    }

    public double TotalWeight => _teachers.Sum(t => t.Weight);
    public bool AllTeachersCompleted => _teachers.All(t => t.IsCompleted);

    public void SetTotalFrames(int total)
    {
        EnsureStatus(RunStatus.Running);
        if (total <= 0) throw new DomainException("Snapshot has no frames");
        TotalFrames = total;
    }

    public void RecordProgress(Guid teacherId, int processedFrames)
    {
        EnsureStatus(RunStatus.Running);
        Teacher(teacherId).ProcessedFrames = Math.Min(processedFrames, TotalFrames);
    }

    public void CompleteTeacher(Guid teacherId)
    {
        EnsureStatus(RunStatus.Running);
        var teacher = Teacher(teacherId);
        teacher.ProcessedFrames = TotalFrames;
        teacher.IsCompleted = true;
    }

    /// <summary>Консенсус посчитан по всем кадрам.</summary>
    public void FinishAggregation(int accepted, int rejected, int needsReview)
    {
        EnsureStatus(RunStatus.Running);
        if (!AllTeachersCompleted) throw new DomainException("Not all teachers have finished");
        if (accepted + rejected + needsReview != TotalFrames) throw new DomainException("Every frame must get exactly one label");

        Accepted = accepted;
        Rejected = rejected;
        PendingReview = needsReview;
        if (needsReview == 0) Finish(RunStatus.Completed);
        else Status = RunStatus.Reviewing;
    }

    /// <returns>true, если это была последняя проверка и прогон завершён.</returns>
    public bool RecordReview(LabelStatus outcome)
    {
        EnsureStatus(RunStatus.Reviewing);
        if (outcome == LabelStatus.NeedsReview) throw new DomainException("Review must produce a final decision");
        PendingReview--;
        Reviewed++;
        if (outcome == LabelStatus.Accepted) Accepted++;
        else Rejected++;

        if (PendingReview > 0) return false;
        Finish(RunStatus.Completed);
        return true;
    }

    public void Fail(string reason)
    {
        if (Status is not (RunStatus.Running or RunStatus.Reviewing)) throw new DomainException($"Cannot fail run in status {Status}");
        FailureReason = reason;
        Finish(RunStatus.Failed);
    }

    /// <returns>false, если прогон уже был отброшен (повторная компенсация).</returns>
    public bool Discard()
    {
        if (Status == RunStatus.Discarded) return false;
        Status = RunStatus.Discarded;
        FinishedAt ??= DateTime.UtcNow;
        return true;
    }

    private void Finish(RunStatus status)
    {
        Status = status;
        FinishedAt = DateTime.UtcNow;
    }

    private RunTeacher Teacher(Guid teacherId) =>
        _teachers.FirstOrDefault(t => t.TeacherId == teacherId) ?? throw new DomainException($"Teacher {teacherId} is not in this run");

    private void EnsureStatus(RunStatus expected)
    {
        if (Status != expected) throw new DomainException($"Run is {Status}, expected {expected}");
    }
}
