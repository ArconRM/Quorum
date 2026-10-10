namespace Training.Domain.Cycles;

/// <summary>Шаг саги. Финальные: Completed, Failed, Skipped.</summary>
public enum CycleStatus
{
    Snapshotting,
    Labeling,
    Training,
    Evaluating,
    Completed,
    Failed,
    Skipped,
}
