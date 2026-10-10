namespace Training.Domain.Cycles;

/// <summary>Что нужно откатить в других сервисах после провала цикла.</summary>
/// <param name="DiscardLabeling">Разметка уже готова — пометить неиспользованной (Labeling).</param>
/// <param name="ReleaseSnapshot">Снимок создан — отбросить, кадры вернуть в «новые» (Datasets).</param>
public sealed record CycleCompensation(bool DiscardLabeling, bool ReleaseSnapshot);
