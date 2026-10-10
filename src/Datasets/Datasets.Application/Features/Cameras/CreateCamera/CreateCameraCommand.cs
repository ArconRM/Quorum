using MediatR;

namespace Datasets.Application.Features.Cameras.CreateCamera;

public sealed record CreateCameraCommand(
    string Name, string? Description, string Task, string Prompt, int MinFramesPerCycle, int CycleIntervalHours) : IRequest<Guid>;
