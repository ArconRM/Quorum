using MediatR;
using Quorum.Application.Exceptions;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Application.Features.Jobs.GetJob;

public sealed class GetJobHandler(ITrainingJobRepository jobs) : IRequestHandler<GetJobQuery, TrainingJobDto>
{
    public async Task<TrainingJobDto> Handle(GetJobQuery q, CancellationToken ct) =>
        TrainingJobDto.From(await jobs.GetAsync(q.JobId, ct) ?? throw new NotFoundException($"Training job {q.JobId} not found"));
}
