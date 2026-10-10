namespace Quorum.Application.Contracts;

// Шаг 2 саги: псевдоразметка ансамблем учителей
public sealed record StartLabeling(Guid CycleId, Guid SnapshotId, string Task, string Prompt, string ValidationMode, double AgreementThreshold);
public sealed record LabelingCompleted(Guid CycleId, Guid RunId, int Accepted, int Rejected, int Reviewed);
public sealed record LabelingFailed(Guid CycleId, Guid? RunId, string Reason);

/// <summary>Компенсация шага 2: разметка помечается неиспользованной, открытые задания на проверку отменяются.</summary>
public sealed record DiscardLabeling(Guid CycleId);
public sealed record LabelingDiscarded(Guid CycleId, Guid RunId);
