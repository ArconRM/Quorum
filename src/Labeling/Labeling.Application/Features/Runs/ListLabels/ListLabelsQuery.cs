using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Runs.ListLabels;

public sealed record ListLabelsQuery(Guid RunId, string? Status, int Skip, int Take) : IRequest<IReadOnlyList<LabelDto>>;
