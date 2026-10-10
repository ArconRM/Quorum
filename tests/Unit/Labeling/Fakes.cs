using System.Runtime.CompilerServices;
using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Labeling.Domain.Predictions;
using Labeling.Domain.Runs;
using Quorum.Application.Exceptions;

namespace Labeling.UnitTests;

internal sealed class InMemoryPredictions : IPredictionRepository
{
    public List<Prediction> Items { get; } = [];

    public Task<IReadOnlyList<Prediction>> ListByRunAsync(Guid runId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Prediction>>(Items.Where(p => p.RunId == runId).ToList());

    public Task<IReadOnlyList<Prediction>> ListByFrameAsync(Guid runId, Guid frameId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Prediction>>(Items.Where(p => p.RunId == runId && p.FrameId == frameId).ToList());

    public Task<IReadOnlySet<Guid>> FramesWithPredictionsAsync(Guid runId, Guid teacherId, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(Items.Where(p => p.RunId == runId && p.TeacherId == teacherId).Select(p => p.FrameId).ToHashSet());

    public void Add(Prediction prediction) => Items.Add(prediction);
}

/// <summary>Воркер-заглушка для тестов: отвечает по заданной функции или «падает».</summary>
internal sealed class FakeTeacherRunner(Func<RunTeacher, SourceFrame, TeacherPrediction>? answer) : ITeacherRunner
{
    public async IAsyncEnumerable<TeacherPrediction> RunAsync(RunTeacher teacher, TaskType task, string prompt,
        IReadOnlyList<SourceFrame> frames, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        if (answer is null) throw new ExternalServiceException("worker is down");
        foreach (var frame in frames) yield return answer(teacher, frame);
    }
}
