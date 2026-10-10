using Quorum.Domain;
using Training.Domain.Common;

namespace Training.Domain.Cycles;

/// <summary>
/// Корень агрегата и одновременно оркестратор саги «цикл адаптации» для одной камеры:
///
///   Snapshotting ──SnapshotCreated──► Labeling ──LabelingCompleted──► Training ──► Evaluating ──► Completed
///        │                               │                              │              │
///   SnapshotRejected → Skipped      LabelingFailed → Failed     сбой обучения → Failed  кандидат хуже → Completed(KeptCurrent)
///
/// Переходы проверяются здесь, а хендлеры по результату переходов отправляют команды и компенсации.
/// Повторная доставка события, не подходящего текущему шагу, игнорируется (идемпотентность).
/// </summary>
public sealed class AdaptationCycle : Entity
{
    private AdaptationCycle() { }

    public Guid CameraId { get; private set; }
    public CycleTrigger Trigger { get; private set; }
    public ValidationSettings Validation { get; private set; } = null!;
    public CycleStatus Status { get; private set; }
    public CycleOutcome? Outcome { get; private set; }
    public TaskType? Task { get; private set; }

    public Guid? SnapshotId { get; private set; }
    public int TrainFrames { get; private set; }
    public int HoldoutFrames { get; private set; }

    public Guid? LabelingRunId { get; private set; }
    public int AcceptedLabels { get; private set; }

    public Guid? TrainingJobId { get; private set; }
    public Guid? CandidateVersionId { get; private set; }

    public string? FailureReason { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public uint Version { get; private set; }

    public bool IsFinished => Status is CycleStatus.Completed or CycleStatus.Failed or CycleStatus.Skipped;

    public static AdaptationCycle Start(Guid cameraId, CycleTrigger trigger, ValidationSettings validation)
    {
        if (cameraId == Guid.Empty) throw new DomainException("Camera is required");
        return new AdaptationCycle
        {
            Id = Guid.NewGuid(),
            CameraId = cameraId,
            Trigger = trigger,
            Validation = validation,
            Status = CycleStatus.Snapshotting,
            StartedAt = DateTime.UtcNow,
        };
    }

    // ---------- шаг 1: снимок ----------

    public void OnSnapshotCreated(Guid snapshotId, TaskType task, int trainFrames, int holdoutFrames)
    {
        Ensure(CycleStatus.Snapshotting);
        SnapshotId = snapshotId;
        Task = task;
        TrainFrames = trainFrames;
        HoldoutFrames = holdoutFrames;
        Status = CycleStatus.Labeling;
    }

    /// <summary>Новых кадров мало или камера занята — цикл не нужен, компенсировать нечего.</summary>
    public void Skip(string reason)
    {
        Ensure(CycleStatus.Snapshotting);
        FailureReason = reason;
        Finish(CycleStatus.Skipped);
    }

    // ---------- шаг 2: разметка ----------

    /// <returns>Компенсации, если принятых меток слишком мало для обучения; null — можно обучать.</returns>
    public CycleCompensation? OnLabelingCompleted(Guid runId, int acceptedLabels, int minTrainingLabels)
    {
        Ensure(CycleStatus.Labeling);
        LabelingRunId = runId;
        AcceptedLabels = acceptedLabels;
        if (acceptedLabels < minTrainingLabels)
            return Fail($"Too few accepted labels for training: {acceptedLabels} < {minTrainingLabels}");

        Status = CycleStatus.Training;
        return null;
    }

    public CycleCompensation OnLabelingFailed(string reason)
    {
        Ensure(CycleStatus.Labeling);
        return Fail($"Labeling failed: {reason}");
    }

    // ---------- шаг 3: обучение ----------

    public void OnTrainingStarted(Guid trainingJobId)
    {
        Ensure(CycleStatus.Training);
        TrainingJobId = trainingJobId;
    }

    public void OnTrainingSucceeded(Guid candidateVersionId)
    {
        Ensure(CycleStatus.Training);
        CandidateVersionId = candidateVersionId;
        Status = CycleStatus.Evaluating;
    }

    public CycleCompensation OnTrainingFailed(string reason)
    {
        Ensure(CycleStatus.Training);
        return Fail($"Training failed: {reason}");
    }

    // ---------- шаг 4: оценка ----------

    public void Complete(bool promoted)
    {
        Ensure(CycleStatus.Evaluating);
        Outcome = promoted ? CycleOutcome.Promoted : CycleOutcome.KeptCurrent;
        Finish(CycleStatus.Completed);
    }

    public CycleCompensation OnEvaluationFailed(string reason)
    {
        Ensure(CycleStatus.Evaluating);
        return Fail($"Evaluation failed: {reason}");
    }

    private CycleCompensation Fail(string reason)
    {
        FailureReason = reason;
        Finish(CycleStatus.Failed);
        return new CycleCompensation(DiscardLabeling: LabelingRunId is not null, ReleaseSnapshot: SnapshotId is not null);
    }

    private void Finish(CycleStatus status)
    {
        Status = status;
        FinishedAt = DateTime.UtcNow;
    }

    private void Ensure(CycleStatus expected)
    {
        if (Status != expected) throw new DomainException($"Cycle is {Status}, expected {expected}");
    }
}
