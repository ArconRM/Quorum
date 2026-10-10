using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Quorum.Domain;

namespace Labeling.Domain.Labels;

/// <summary>Корень агрегата: итоговая метка кадра в прогоне (одна на кадр).</summary>
public sealed class ConsensusLabel : Entity
{
    private List<BoundingBox> _boxes = [];

    private ConsensusLabel() { }

    public Guid RunId { get; private set; }
    public Guid FrameId { get; private set; }
    public string FramePath { get; private set; } = string.Empty;
    public DataSplit Split { get; private set; }
    public TaskType Task { get; private set; }
    public string? Text { get; private set; }
    public double Agreement { get; private set; }
    public LabelStatus Status { get; private set; }
    public LabelSource Source { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public IReadOnlyList<BoundingBox> Boxes => _boxes;

    public static ConsensusLabel Create(Guid runId, Guid frameId, string framePath, DataSplit split, TaskType task,
        ConsensusResult consensus, LabelStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        FrameId = frameId,
        FramePath = framePath,
        Split = split,
        Task = task,
        _boxes = consensus.Boxes.ToList(),
        Text = consensus.Text,
        Agreement = consensus.Agreement,
        Status = status,
        Source = LabelSource.Consensus,
    };

    /// <summary>Решение разметчика. Для Correct нужна новая метка: боксы (детекция) или текст (OCR).</summary>
    public void ApplyReview(Guid reviewerId, ReviewVerdict verdict, IReadOnlyList<BoundingBox>? boxes = null, string? text = null)
    {
        if (Status != LabelStatus.NeedsReview) throw new DomainException($"Label is already {Status}");

        switch (verdict)
        {
            case ReviewVerdict.Approve:
                Status = LabelStatus.Accepted;
                break;
            case ReviewVerdict.Reject:
                Status = LabelStatus.Rejected;
                break;
            case ReviewVerdict.Correct when Task == TaskType.Detection:
                _boxes = (boxes ?? throw new DomainException("Corrected boxes are required")).ToList();
                Source = LabelSource.Human;
                Status = LabelStatus.Accepted;
                break;
            case ReviewVerdict.Correct:
                var normalized = TextConsensus.Normalize(text);
                if (normalized.Length == 0) throw new DomainException("Corrected text is required");
                Text = normalized;
                Source = LabelSource.Human;
                Status = LabelStatus.Accepted;
                break;
        }

        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }
}
