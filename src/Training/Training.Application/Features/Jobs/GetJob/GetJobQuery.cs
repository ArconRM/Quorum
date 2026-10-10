using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Jobs.GetJob;

public sealed record GetJobQuery(Guid JobId) : IRequest<TrainingJobDto>;
