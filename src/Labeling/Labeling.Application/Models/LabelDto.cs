using Labeling.Domain.Labels;

namespace Labeling.Application.Models;

public sealed record LabelDto(Guid Id, Guid FrameId, string FramePath, string Split, IReadOnlyList<BoxDto> Boxes, string? Text,
    double Agreement, string Status, string Source, Guid? ReviewedBy)
{
    public static LabelDto From(ConsensusLabel l) => new(
        l.Id, l.FrameId, l.FramePath, l.Split.ToString(), l.Boxes.Select(BoxDto.From).ToList(), l.Text,
        l.Agreement, l.Status.ToString(), l.Source.ToString(), l.ReviewedBy);
}

public sealed record PredictionDto(Guid TeacherId, string Teacher, double Weight, IReadOnlyList<BoxDto> Boxes, string? Text, double Confidence);

/// <summary>Всё, что нужно разметчику: подсказка консенсуса и ответы каждого учителя. Картинка — GET /datasets/frames/{frameId}/content.</summary>
public sealed record ReviewDto(Guid TaskId, Guid RunId, string Task, string Status, LabelDto Suggestion, IReadOnlyList<PredictionDto> Predictions);

public sealed record TrainingItemDto(Guid FrameId, string Path, string Split, IReadOnlyList<BoxDto> Boxes, string? Text);

public sealed record TrainingSetDto(string Task, IReadOnlyList<TrainingItemDto> Items);
