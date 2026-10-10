using System.Runtime.CompilerServices;
using Grpc.Core;
using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Runs;
using Quorum.Application.Exceptions;
using Quorum.Grpc.Worker;

namespace Labeling.Infrastructure.Grpc;

/// <summary>gRPC-клиент к GPU-воркеру: стрим предсказаний одного учителя.</summary>
internal sealed class GrpcTeacherRunner(InferenceWorker.InferenceWorkerClient client) : ITeacherRunner
{
    public async IAsyncEnumerable<TeacherPrediction> RunAsync(RunTeacher teacher, TaskType task, string prompt,
        IReadOnlyList<SourceFrame> frames, [EnumeratorCancellation] CancellationToken ct)
    {
        if (frames.Count == 0) yield break;

        var request = new RunTeacherRequest
        {
            Teacher = teacher.Name,
            Kind = teacher.Kind.ToString(),
            Task = task.ToString(),
            Prompt = prompt,
        };
        request.Frames.AddRange(frames.Select(f => new FrameRef { FrameId = f.FrameId.ToString(), Path = f.Path }));

        using var call = client.RunTeacher(request, deadline: DateTime.UtcNow.AddHours(6), cancellationToken: ct);
        await using var stream = call.ResponseStream.ReadAllAsync(ct).GetAsyncEnumerator(ct);

        while (true)
        {
            // yield нельзя ставить внутри try/catch, поэтому ошибки ловим только вокруг чтения.
            FramePrediction current;
            try
            {
                if (!await stream.MoveNextAsync()) yield break;
                current = stream.Current;
            }
            catch (RpcException ex)
            {
                throw new ExternalServiceException($"Inference worker failed on teacher '{teacher.Name}': {ex.Status.Detail}", ex);
            }

            yield return new TeacherPrediction(
                Guid.Parse(current.FrameId),
                current.Boxes.Select(ToBox).OfType<BoundingBox>().ToList(),
                current.Text,
                current.Confidence);
        }
    }

    /// <summary>Модели иногда вылезают за край кадра на доли пикселя — обрезаем, вырожденные боксы отбрасываем.</summary>
    private static BoundingBox? ToBox(Box b)
    {
        var x = Math.Clamp(b.X, 0, 1);
        var y = Math.Clamp(b.Y, 0, 1);
        var w = Math.Min(b.W, 1 - x);
        var h = Math.Min(b.H, 1 - y);
        return w > 0 && h > 0 ? new BoundingBox(x, y, w, h, Math.Clamp(b.Score, 0, 1)) : null;
    }
}
