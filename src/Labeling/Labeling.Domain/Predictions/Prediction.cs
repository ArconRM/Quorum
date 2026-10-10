using Labeling.Domain.Consensus;
using Quorum.Domain;

namespace Labeling.Domain.Predictions;

/// <summary>
/// Корень агрегата: ответ одного учителя на один кадр. Хранится всегда — это кэш, на котором
/// агрегацию можно переиграть с другими порогами и весами без повторного инференса.
/// </summary>
public sealed class Prediction : Entity
{
    private List<BoundingBox> _boxes = [];

    private Prediction() { }

    public Guid RunId { get; private set; }
    public Guid FrameId { get; private set; }
    public Guid TeacherId { get; private set; }
    public string? Text { get; private set; }
    public double Confidence { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyList<BoundingBox> Boxes => _boxes;

    public static Prediction Create(Guid runId, Guid frameId, Guid teacherId, IEnumerable<BoundingBox> boxes, string? text, double confidence)
    {
        if (runId == Guid.Empty || frameId == Guid.Empty || teacherId == Guid.Empty)
            throw new DomainException("Run, frame and teacher are required");
        return new Prediction
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            FrameId = frameId,
            TeacherId = teacherId,
            _boxes = boxes.ToList(),
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            Confidence = Math.Clamp(confidence, 0, 1),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
