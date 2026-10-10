using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Snapshots.GetSnapshot;

public sealed class GetSnapshotHandler(ISnapshotRepository snapshots) : IRequestHandler<GetSnapshotQuery, SnapshotDto>
{
    public async Task<SnapshotDto> Handle(GetSnapshotQuery q, CancellationToken ct) =>
        SnapshotDto.From(await snapshots.GetAsync(q.SnapshotId, ct) ?? throw new NotFoundException($"Snapshot {q.SnapshotId} not found"));
}
