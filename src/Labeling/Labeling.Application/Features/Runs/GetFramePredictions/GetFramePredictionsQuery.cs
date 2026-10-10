using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Runs.GetFramePredictions;

/// <summary>Ответы всех учителей на кадр — чтобы видеть, кто с кем не согласился.</summary>
public sealed record GetFramePredictionsQuery(Guid RunId, Guid FrameId) : IRequest<IReadOnlyList<PredictionDto>>;
