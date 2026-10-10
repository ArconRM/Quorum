using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Runs.GetTrainingSet;

/// <summary>Принятые метки завершённого прогона — данные для обучения и оценки студента (Training, gRPC).</summary>
public sealed record GetTrainingSetQuery(Guid RunId) : IRequest<TrainingSetDto>;
