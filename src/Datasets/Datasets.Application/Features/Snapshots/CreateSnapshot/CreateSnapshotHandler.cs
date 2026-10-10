using Datasets.Application.Abstractions;
using Datasets.Domain.Snapshots;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Datasets.Application.Features.Snapshots.CreateSnapshot;

public sealed class CreateSnapshotHandler(
    ICameraRepository cameras,
    IFrameRepository frames,
    ISnapshotRepository snapshots,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<CreateSnapshotCommand>
{
    /// <summary>Потолок одного снимка; остальные кадры подождут следующего цикла.</summary>
    public const int MaxFramesPerSnapshot = 5000;

    public async Task Handle(CreateSnapshotCommand cmd, CancellationToken ct)
    {
        // Идемпотентность: повторная команда по тому же циклу повторяет ответ.
        var existing = await snapshots.GetByCycleAsync(cmd.CycleId, ct);
        if (existing is not null)
        {
            await events.PublishAsync(Created(existing), ct);
            await uow.CommitAsync(ct);
            return;
        }

        var camera = await cameras.GetAsync(cmd.CameraId, ct);
        string? rejection = null;
        if (camera is null) rejection = $"Camera {cmd.CameraId} not found";
        else if (!camera.IsActive) rejection = $"Camera '{camera.Name}' is inactive";
        else if (await snapshots.HasLockedAsync(camera.Id, ct)) rejection = "Another adaptation cycle is in progress for this camera";

        if (rejection is null)
        {
            var newFrames = await frames.TakeNewAsync(camera!.Id, MaxFramesPerSnapshot, ct);
            if (!camera.HasEnoughFrames(newFrames.Count))
            {
                rejection = $"Not enough new frames: {newFrames.Count} < {camera.MinFramesPerCycle}";
            }
            else
            {
                var snapshot = DatasetSnapshot.Create(camera, cmd.CycleId, newFrames.Select(f => (f.Id, f.StoragePath)).ToList());
                foreach (var frame in newFrames) frame.AttachTo(snapshot.Id);
                snapshots.Add(snapshot);
                await events.PublishAsync(Created(snapshot), ct);
                await uow.CommitAsync(ct);
                return;
            }
        }

        await events.PublishAsync(new SnapshotRejected(cmd.CycleId, cmd.CameraId, rejection), ct);
        await uow.CommitAsync(ct);
    }

    private static SnapshotCreated Created(DatasetSnapshot s) =>
        new(s.CycleId, s.Id, s.CameraId, s.Task.ToString(), s.Prompt, s.TrainCount, s.HoldoutCount);
}
