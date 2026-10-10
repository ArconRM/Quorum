using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Snapshots.GetSnapshot;

public sealed record GetSnapshotQuery(Guid SnapshotId) : IRequest<SnapshotDto>;
