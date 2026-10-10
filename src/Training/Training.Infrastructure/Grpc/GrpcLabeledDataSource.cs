using Grpc.Core;
using Quorum.Application.Exceptions;
using Quorum.Grpc.Labeling;
using Training.Application.Abstractions;

namespace Training.Infrastructure.Grpc;

/// <summary>gRPC-клиент к Labeling за портом Application-слоя.</summary>
internal sealed class GrpcLabeledDataSource(TrainingSetQuery.TrainingSetQueryClient client) : ILabeledDataSource
{
    public async Task<IReadOnlyList<LabeledSample>> GetAcceptedAsync(Guid labelingRunId, CancellationToken ct)
    {
        try
        {
            var response = await client.GetTrainingSetAsync(new GetTrainingSetRequest { RunId = labelingRunId.ToString() },
                deadline: DateTime.UtcNow.AddMinutes(2), cancellationToken: ct);
            return response.Frames.Select(f => new LabeledSample(
                    Guid.Parse(f.FrameId),
                    f.Path,
                    string.Equals(f.Split, "Holdout", StringComparison.OrdinalIgnoreCase),
                    f.Boxes.Select(b => new LabeledBox(b.X, b.Y, b.W, b.H)).ToList(),
                    string.IsNullOrEmpty(f.Text) ? null : f.Text))
                .ToList();
        }
        catch (RpcException ex)
        {
            throw new ExternalServiceException($"Labeling service failed: {ex.Status.Detail}", ex);
        }
    }
}
