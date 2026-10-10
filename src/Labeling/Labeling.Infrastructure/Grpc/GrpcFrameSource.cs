using Grpc.Core;
using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Quorum.Application.Exceptions;
using Quorum.Grpc.Datasets;

namespace Labeling.Infrastructure.Grpc;

/// <summary>gRPC-клиент к Datasets за портом Application-слоя.</summary>
internal sealed class GrpcFrameSource(SnapshotQuery.SnapshotQueryClient client) : IFrameSource
{
    public async Task<IReadOnlyList<SourceFrame>> GetSnapshotFramesAsync(Guid snapshotId, CancellationToken ct)
    {
        try
        {
            var response = await client.GetSnapshotFramesAsync(
                new GetSnapshotFramesRequest { SnapshotId = snapshotId.ToString() },
                deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: ct);
            return response.Frames
                .Select(f => new SourceFrame(Guid.Parse(f.FrameId), f.Path, Enum.Parse<DataSplit>(f.Split, ignoreCase: true)))
                .ToList();
        }
        catch (RpcException ex)
        {
            throw new ExternalServiceException($"Datasets service failed: {ex.Status.Detail}", ex);
        }
    }
}
