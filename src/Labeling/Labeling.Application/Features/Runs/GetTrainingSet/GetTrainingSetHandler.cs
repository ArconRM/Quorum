using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using Labeling.Domain.Runs;
using MediatR;
using Quorum.Application.Exceptions;
using Quorum.Domain;

namespace Labeling.Application.Features.Runs.GetTrainingSet;

public sealed class GetTrainingSetHandler(IRunRepository runs, ILabelRepository labels) : IRequestHandler<GetTrainingSetQuery, TrainingSetDto>
{
    public async Task<TrainingSetDto> Handle(GetTrainingSetQuery q, CancellationToken ct)
    {
        var run = await runs.GetAsync(q.RunId, ct) ?? throw new NotFoundException($"Run {q.RunId} not found");
        if (run.Status != RunStatus.Completed) throw new DomainException($"Run is {run.Status}, training set is not ready");

        var items = (await labels.ListAcceptedAsync(run.Id, ct))
            .Select(l => new TrainingItemDto(l.FrameId, l.FramePath, l.Split.ToString(), l.Boxes.Select(BoxDto.From).ToList(), l.Text))
            .ToList();
        return new TrainingSetDto(run.Task.ToString(), items);
    }
}
