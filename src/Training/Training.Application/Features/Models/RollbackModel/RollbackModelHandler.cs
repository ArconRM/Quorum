using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Application.Exceptions;
using Training.Application.Abstractions;
using Training.Application.Models;
using Training.Domain.Models;

namespace Training.Application.Features.Models.RollbackModel;

public sealed class RollbackModelHandler(
    IModelVersionRepository versions,
    IPublishedModelCache cache,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<RollbackModelCommand, ModelVersionDto>
{
    public async Task<ModelVersionDto> Handle(RollbackModelCommand cmd, CancellationToken ct)
    {
        var target = await versions.GetAsync(cmd.VersionId, ct) ?? throw new NotFoundException($"Version {cmd.VersionId} not found");
        var current = await versions.GetPublishedAsync(target.CameraId, ct);

        if (target.Status != ModelVersionStatus.Archived)
            throw new Quorum.Domain.DomainException($"Only archived versions can be restored, this one is {target.Status}");
        if (current is not null)
        {
            // Сначала архив текущей, потом публикация целевой: индекс «одна опубликованная» проверяется построчно.
            ModelPublication.Retire(current);
            await uow.CommitAsync(ct);
        }
        ModelPublication.Restore(target, current);
        await events.PublishAsync(new ModelPublished(target.CameraId, target.Id, target.Number, target.MetricName,
            target.HoldoutMetric ?? 0), ct);
        await uow.CommitAsync(ct);
        await cache.InvalidateAsync(target.CameraId, ct);
        return ModelVersionDto.From(target);
    }
}