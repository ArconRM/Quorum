using Labeling.Application.Abstractions;
using Labeling.Application.Messages;
using Labeling.Domain.Common;
using Labeling.Domain.Runs;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Domain;

namespace Labeling.Application.Features.Runs.StartLabelingRun;

public sealed class StartLabelingRunHandler(
    ITeacherRepository teachers,
    IRunRepository runs,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<StartLabelingRunCommand>
{
    public async Task Handle(StartLabelingRunCommand cmd, CancellationToken ct)
    {
        // Идемпотентность: один прогон на цикл; повторная команда повторяет итог, если он уже есть.
        var existing = await runs.GetByCycleAsync(cmd.CycleId, ct);
        if (existing is not null)
        {
            if (existing.Status == RunStatus.Completed)
                await events.PublishAsync(new LabelingCompleted(cmd.CycleId, existing.Id, existing.Accepted, existing.Rejected, existing.Reviewed), ct);
            else if (existing.Status == RunStatus.Failed)
                await events.PublishAsync(new LabelingFailed(cmd.CycleId, existing.Id, existing.FailureReason ?? "Labeling failed"), ct);
            await uow.CommitAsync(ct);
            return;
        }

        try
        {
            if (!Enum.TryParse<TaskType>(cmd.Task, true, out var task)) throw new DomainException($"Unknown task '{cmd.Task}'");
            if (!Enum.TryParse<ValidationMode>(cmd.ValidationMode, true, out var mode))
                throw new DomainException($"Unknown validation mode '{cmd.ValidationMode}'");

            var active = await teachers.ListAsync(task, onlyActive: true, ct);
            var run = LabelingRun.Start(cmd.CycleId, cmd.SnapshotId, task, cmd.Prompt,
                ValidationStrategy.Create(mode, cmd.AgreementThreshold), active);
            runs.Add(run);
            await events.PublishAsync(new Messages.ProcessLabelingRun(run.Id), ct);
        }
        catch (DomainException ex)
        {
            // Бизнес-отказ — не ошибка шины, а событие: сага запустит компенсации.
            await events.PublishAsync(new LabelingFailed(cmd.CycleId, null, ex.Message), ct);
        }

        await uow.CommitAsync(ct);
    }
}
