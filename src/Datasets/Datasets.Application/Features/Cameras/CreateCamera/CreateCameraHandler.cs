using Datasets.Application.Abstractions;
using Datasets.Domain.Cameras;
using MediatR;
using Quorum.Application.Abstractions;

namespace Datasets.Application.Features.Cameras.CreateCamera;

public sealed class CreateCameraHandler(ICameraRepository cameras, IUnitOfWork uow) : IRequestHandler<CreateCameraCommand, Guid>
{
    public async Task<Guid> Handle(CreateCameraCommand cmd, CancellationToken ct)
    {
        var task = Enum.Parse<TaskType>(cmd.Task, ignoreCase: true);
        var camera = Camera.Create(cmd.Name, cmd.Description ?? string.Empty, task, cmd.Prompt, cmd.MinFramesPerCycle, cmd.CycleIntervalHours);
        cameras.Add(camera);
        await uow.CommitAsync(ct);
        return camera.Id;
    }
}
