using Quorum.Domain;

namespace Training.Domain.Evaluations;

/// <summary>
/// Корень агрегата: сравнение кандидата с опубликованной моделью на отложенной выборке цикла.
/// Правило: публикуем, если опубликованной модели нет или кандидат лучше не меньше чем на MinImprovement.
/// Обе модели оцениваются на одних и тех же новых кадрах — это и есть проверка «стало ли лучше на текущих условиях».
/// </summary>
public sealed class Evaluation : Entity
{
    private Evaluation() { }

    public Guid CycleId { get; private set; }
    public Guid CandidateVersionId { get; private set; }
    public Guid? BaselineVersionId { get; private set; }
    public string MetricName { get; private set; } = string.Empty;
    public double CandidateScore { get; private set; }
    public double? BaselineScore { get; private set; }
    public int HoldoutSize { get; private set; }
    public double MinImprovement { get; private set; }
    public EvaluationDecision Decision { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Evaluation Decide(Guid cycleId, Guid candidateVersionId, Guid? baselineVersionId, string metricName,
        double candidateScore, double? baselineScore, int holdoutSize, double minImprovement)
    {
        if (minImprovement < 0) throw new DomainException("Minimal improvement cannot be negative");
        if (baselineVersionId is not null && baselineScore is null)
            throw new DomainException("Baseline score is required when there is a published model");

        var promote = baselineScore is null || candidateScore >= baselineScore.Value + minImprovement;
        return new Evaluation
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            CandidateVersionId = candidateVersionId,
            BaselineVersionId = baselineVersionId,
            MetricName = metricName,
            CandidateScore = candidateScore,
            BaselineScore = baselineScore,
            HoldoutSize = holdoutSize,
            MinImprovement = minImprovement,
            Decision = promote ? EvaluationDecision.Promote : EvaluationDecision.Reject,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
