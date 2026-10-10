namespace Training.Application.Abstractions;

public sealed record LabeledBox(double X, double Y, double W, double H);

public sealed record LabeledSample(Guid FrameId, string Path, bool IsHoldout, IReadOnlyList<LabeledBox> Boxes, string? Text);

/// <summary>Порт к Labeling: принятые метки прогона. Бросает ExternalServiceException, если Labeling недоступен.</summary>
public interface ILabeledDataSource
{
    Task<IReadOnlyList<LabeledSample>> GetAcceptedAsync(Guid labelingRunId, CancellationToken ct);
}
