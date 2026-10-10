using MediatR;

namespace Labeling.Application.Features.Runs.StartLabelingRun;

/// <summary>Шаг 2 саги: начать псевдоразметку снимка ансамблем активных учителей.</summary>
public sealed record StartLabelingRunCommand(
    Guid CycleId, Guid SnapshotId, string Task, string Prompt, string ValidationMode, double AgreementThreshold) : IRequest;
