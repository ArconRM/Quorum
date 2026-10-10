using MediatR;

namespace Labeling.Application.Features.Runs.DiscardLabelingRun;

/// <summary>Компенсация шага 2: разметка цикла не будет использована, открытые проверки отменяются.</summary>
public sealed record DiscardLabelingRunCommand(Guid CycleId) : IRequest;
