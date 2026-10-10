using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Training.Domain.Cycles;

namespace Training.Application.Sagas;

/// <summary>Отправка компенсирующих команд, которые домен цикла вернул при провале.</summary>
internal static class Compensations
{
    public static async Task PublishAsync(IIntegrationEventPublisher events, AdaptationCycle cycle,
        CycleCompensation compensation, CancellationToken ct)
    {
        if (compensation.DiscardLabeling)
            await events.PublishAsync(new DiscardLabeling(cycle.Id), ct);
        if (compensation.ReleaseSnapshot && cycle.SnapshotId is { } snapshotId)
            await events.PublishAsync(new ReleaseSnapshot(cycle.Id, snapshotId, Discard: true), ct);
    }
}
