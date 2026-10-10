using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Snapshots.GetSnapshotFrames;

public sealed class GetSnapshotFramesHandler(ISnapshotRepository snapshots)
    : IRequestHandler<GetSnapshotFramesQuery, IReadOnlyList<SnapshotFrameDto>>
{
    public async Task<IReadOnlyList<SnapshotFrameDto>> Handle(GetSnapshotFramesQuery q, CancellationToken ct)
    {
        var snapshot = await snapshots.GetAsync(q.SnapshotId, ct) ?? throw new NotFoundException($"Snapshot {q.SnapshotId} not found");
        return snapshot.Frames.Select(f => new SnapshotFrameDto(f.FrameId, f.StoragePath, f.Split.ToString())).ToList();
    }
}
