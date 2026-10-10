using Grpc.Core;
using Quorum.Application.Exceptions;
using Quorum.Grpc.Worker;
using Training.Application.Abstractions;
using Training.Domain.Jobs;

namespace Training.Infrastructure.Grpc;

/// <summary>gRPC-клиент к GPU-воркеру: обучение (стрим эпох) и оценка.</summary>
internal sealed class GrpcTrainingWorker(InferenceWorker.InferenceWorkerClient client) : ITrainingWorker
{
    public async Task<TrainingResult> TrainAsync(TrainingJob job, IReadOnlyList<LabeledSample> trainSet,
        Func<int, double, Task> onEpoch, CancellationToken ct)
    {
        var request = new TrainRequest
        {
            JobId = job.Id.ToString(),
            Task = job.Task.ToString(),
            Architecture = job.Architecture,
            Epochs = job.Epochs,
            BaseArtifact = job.BaseArtifact ?? string.Empty,
        };
        request.Frames.AddRange(trainSet.Select(ToFrame));

        try
        {
            using var call = client.TrainStudent(request, deadline: DateTime.UtcNow.AddHours(12), cancellationToken: ct);
            await foreach (var progress in call.ResponseStream.ReadAllAsync(ct))
            {
                if (progress.Done) return new TrainingResult(progress.Artifact, progress.MetricName, progress.MetricValue);
                await onEpoch(progress.Epoch, progress.Loss);
            }
        }
        catch (RpcException ex)
        {
            throw new ExternalServiceException($"Inference worker failed to train: {ex.Status.Detail}", ex);
        }

        throw new ExternalServiceException("Inference worker closed the training stream without a result");
    }

    public async Task<EvaluationScores> EvaluateAsync(string task, string candidateArtifact, string? baselineArtifact,
        IReadOnlyList<LabeledSample> holdout, CancellationToken ct)
    {
        var request = new EvaluateRequest
        {
            Task = task,
            CandidateArtifact = candidateArtifact,
            BaselineArtifact = baselineArtifact ?? string.Empty,
        };
        request.Holdout.AddRange(holdout.Select(ToFrame));

        try
        {
            var response = await client.EvaluateAsync(request, deadline: DateTime.UtcNow.AddHours(1), cancellationToken: ct);
            return new EvaluationScores(response.MetricName, response.Candidate, response.HasBaseline ? response.Baseline : null);
        }
        catch (RpcException ex)
        {
            throw new ExternalServiceException($"Inference worker failed to evaluate: {ex.Status.Detail}", ex);
        }
    }

    private static TrainFrame ToFrame(LabeledSample s)
    {
        var frame = new TrainFrame { FrameId = s.FrameId.ToString(), Path = s.Path, Text = s.Text ?? string.Empty };
        frame.Boxes.AddRange(s.Boxes.Select(b => new Box { X = b.X, Y = b.Y, W = b.W, H = b.H, Score = 1 }));
        return frame;
    }
}
