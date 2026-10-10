using Quorum.Domain;

namespace Labeling.Domain.Consensus;

/// <summary>
/// Консенсус детекторов (упрощённый Weighted Box Fusion + кворум):
/// 1. Все боксы всех учителей сортируются по score × вес учителя.
/// 2. Жадная кластеризация: бокс попадает в кластер, если IoU с его представителем ≥ порога
///    и от этого учителя в кластере ещё нет бокса (один учитель — один голос за объект).
/// 3. Поддержка кластера = суммарный вес учителей в нём / суммарный вес всех учителей.
///    Объект принимается, если за него набран кворум: поддержка строго больше половины.
/// 4. Координаты принятого объекта — среднее координат кластера, взвешенное по score × вес;
///    score итогового бокса = поддержка.
/// 5. Согласие кадра = среднее по кластерам max(поддержка, 1 − поддержка): каждый кластер —
///    вопрос «есть ли тут объект», и важно, насколько решительно ансамбль на него ответил.
///    Если ни один учитель ничего не нашёл — согласие 1 (все согласны, что объектов нет).
/// </summary>
public static class BoxConsensus
{
    public const double DefaultIouThreshold = 0.5;
    public const double Quorum = 0.5;

    public static ConsensusResult Compute(IReadOnlyList<TeacherVote> votes, double iouThreshold = DefaultIouThreshold)
    {
        if (votes.Count == 0) throw new DomainException("At least one teacher vote is required");
        if (votes.Any(v => v.Weight <= 0)) throw new DomainException("Teacher weights must be positive");
        var totalWeight = votes.Sum(v => v.Weight);

        var candidates = votes
            .SelectMany(v => v.Boxes.Select(box => (Vote: v, Box: box)))
            .OrderByDescending(c => c.Box.Score * c.Vote.Weight)
            .ToList();

        var clusters = new List<List<(TeacherVote Vote, BoundingBox Box)>>();
        foreach (var candidate in candidates)
        {
            var best = clusters
                .Where(c => c.All(m => m.Vote.TeacherId != candidate.Vote.TeacherId))
                .Select(c => (Cluster: c, IoU: c[0].Box.IoU(candidate.Box)))
                .Where(x => x.IoU >= iouThreshold)
                .OrderByDescending(x => x.IoU)
                .FirstOrDefault();

            if (best.Cluster is not null) best.Cluster.Add(candidate);
            else clusters.Add([candidate]);
        }

        if (clusters.Count == 0) return new ConsensusResult([], null, 1.0);

        var accepted = new List<BoundingBox>();
        var decisiveness = 0.0;
        foreach (var cluster in clusters)
        {
            var support = cluster.Sum(m => m.Vote.Weight) / totalWeight;
            decisiveness += Math.Max(support, 1 - support);
            if (support > Quorum) accepted.Add(Fuse(cluster, support));
        }

        return new ConsensusResult(accepted, null, Math.Round(decisiveness / clusters.Count, 4));
    }

    private static BoundingBox Fuse(List<(TeacherVote Vote, BoundingBox Box)> cluster, double support)
    {
        var weights = cluster.Select(m => Math.Max(m.Box.Score, 1e-6) * m.Vote.Weight).ToList();
        var sum = weights.Sum();
        double Avg(Func<BoundingBox, double> f) => cluster.Select((m, i) => f(m.Box) * weights[i]).Sum() / sum;

        var x = Avg(b => b.X);
        var y = Avg(b => b.Y);
        var w = Math.Min(Avg(b => b.W), 1 - x);
        var h = Math.Min(Avg(b => b.H), 1 - y);
        return new BoundingBox(x, y, w, h, Math.Min(1.0, support));
    }
}
