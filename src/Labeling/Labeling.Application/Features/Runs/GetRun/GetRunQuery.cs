using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Runs.GetRun;

/// <summary>По id прогона или по id цикла адаптации (ровно одно из двух).</summary>
public sealed record GetRunQuery(Guid? RunId, Guid? CycleId) : IRequest<RunDto>;
