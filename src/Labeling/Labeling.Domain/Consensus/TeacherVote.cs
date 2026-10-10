namespace Labeling.Domain.Consensus;

/// <summary>Ответ одного учителя на один кадр вместе с его весом в голосовании.</summary>
public sealed record TeacherVote(Guid TeacherId, double Weight, IReadOnlyList<BoundingBox> Boxes, string? Text);

/// <summary>
/// Итог голосования по кадру. Agreement ∈ [0, 1] — насколько единодушен ансамбль:
/// 1 — все учителя ответили одно и то же, около 0.5 — голоса разделились.
/// </summary>
public sealed record ConsensusResult(IReadOnlyList<BoundingBox> Boxes, string? Text, double Agreement);
