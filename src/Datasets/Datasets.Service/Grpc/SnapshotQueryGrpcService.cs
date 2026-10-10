using Datasets.Application.Features.Snapshots.GetSnapshotFrames;
using Grpc.Core;
using MediatR;
using Quorum.Application.Exceptions;
using Quorum.Grpc.Datasets;

namespace Datasets.Service.Grpc;

/// <summary>gRPC-сервер: состав снимка для Labeling.</summary>
public sealed class SnapshotQueryGrpcService(ISender sender) : SnapshotQuery.SnapshotQueryBase
{
    public override async Task<GetSnapshotFramesResponse> GetSnapshotFrames(GetSnapshotFramesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SnapshotId, out var snapshotId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "snapshot_id must be a GUID"));

        try
        {
            var frames = await sender.Send(new GetSnapshotFramesQuery(snapshotId), context.CancellationToken);
            var response = new GetSnapshotFramesResponse();
            response.Frames.AddRange(frames.Select(f => new SnapshotFrame
            {
                FrameId = f.FrameId.ToString(),
                Path = f.StoragePath,
                Split = f.Split,
            }));
            return response;
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }
}
