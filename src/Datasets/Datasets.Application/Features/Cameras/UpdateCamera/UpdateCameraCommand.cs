using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Cameras.UpdateCamera;

/// <summary>Изменение запроса для учителей, расписания и активности. Null — поле не меняется.</summary>
public sealed record UpdateCameraCommand(
    Guid CameraId, string? Prompt, int? MinFramesPerCycle, int? CycleIntervalHours, bool? IsActive) : IRequest<CameraDto>;
