using Datasets.Domain.Cameras;

namespace Datasets.Application.Models;

public sealed record CameraDto(
    Guid Id, string Name, string Description, string Task, string Prompt,
    int MinFramesPerCycle, int CycleIntervalHours, bool IsActive, DateTime? LastCycleRequestedAt, int NewFrames)
{
    public static CameraDto From(Camera c, int newFrames) => new(
        c.Id, c.Name, c.Description, c.Task.ToString(), c.Prompt,
        c.MinFramesPerCycle, c.CycleIntervalHours, c.IsActive, c.LastCycleRequestedAt, newFrames);
}
