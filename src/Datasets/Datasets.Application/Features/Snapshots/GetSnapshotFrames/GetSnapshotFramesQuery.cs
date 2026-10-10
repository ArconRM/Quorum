using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Snapshots.GetSnapshotFrames;

public sealed record GetSnapshotFramesQuery(Guid SnapshotId) : IRequest<IReadOnlyList<SnapshotFrameDto>>;
