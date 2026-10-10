using MediatR;

namespace Training.Application.Features.Cycles.EvaluateCandidate;

public sealed record EvaluateCandidateCommand(Guid CycleId) : IRequest;
