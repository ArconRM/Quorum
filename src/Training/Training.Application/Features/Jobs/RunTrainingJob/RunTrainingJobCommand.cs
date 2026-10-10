using MediatR;

namespace Training.Application.Features.Jobs.RunTrainingJob;

public sealed record RunTrainingJobCommand(Guid JobId) : IRequest;
