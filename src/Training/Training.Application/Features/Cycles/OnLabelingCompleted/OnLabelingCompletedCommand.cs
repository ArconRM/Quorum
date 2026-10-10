using MediatR;

namespace Training.Application.Features.Cycles.OnLabelingCompleted;

public sealed record OnLabelingCompletedCommand(Guid CycleId, Guid RunId, int Accepted) : IRequest;
