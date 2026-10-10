using MediatR;

namespace Labeling.Application.Features.Runs.ProcessLabelingRun;

public sealed record ProcessLabelingRunCommand(Guid RunId) : IRequest;
