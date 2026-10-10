using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Runs;

namespace Labeling.Application.Abstractions;

public sealed record TeacherPrediction(Guid FrameId, IReadOnlyList<BoundingBox> Boxes, string? Text, double Confidence);

/// <summary>
/// Порт к GPU-воркеру: прогнать одного учителя по кадрам. Ответы приходят потоком по мере готовности.
/// Бросает ExternalServiceException, если воркер недоступен.
/// </summary>
public interface ITeacherRunner
{
    IAsyncEnumerable<TeacherPrediction> RunAsync(RunTeacher teacher, TaskType task, string prompt,
        IReadOnlyList<SourceFrame> frames, CancellationToken ct);
}
