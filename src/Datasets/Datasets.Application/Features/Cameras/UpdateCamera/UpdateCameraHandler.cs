using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Cameras.UpdateCamera;

public sealed class UpdateCameraHandler(ICameraRepository cameras, IFrameRepository frames, IUnitOfWork uow)
    : IRequestHandler<UpdateCameraCommand, CameraDto>
{
    public async Task<CameraDto> Handle(UpdateCameraCommand cmd, CancellationToken ct)
    {
        var camera = await cameras.GetAsync(cmd.CameraId, ct) ?? throw new NotFoundException($"Camera {cmd.CameraId} not found");

        if (cmd.Prompt is not null) camera.ChangePrompt(cmd.Prompt);
        if (cmd.MinFramesPerCycle is not null || cmd.CycleIntervalHours is not null)
            camera.ChangeSchedule(cmd.MinFramesPerCycle ?? camera.MinFramesPerCycle, cmd.CycleIntervalHours ?? camera.CycleIntervalHours);
        if (cmd.IsActive == true) camera.Activate();
        if (cmd.IsActive == false) camera.Deactivate();

        await uow.CommitAsync(ct);
        return CameraDto.From(camera, await frames.CountNewAsync(camera.Id, ct));
    }
}
