using MediatR;

namespace Training.Application.Features.Cycles.OnLabelingFailed;

public sealed record OnLabelingFailedCommand(Guid CycleId, string Reason) : IRequest;
