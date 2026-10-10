using System.Text;
using Quorum.Domain;

namespace Labeling.Domain.Consensus;

/// <summary>
/// Консенсус распознавателей текста (взвешенное голосование):
/// 1. Ответы нормализуются: верхний регистр, без пробелов и разделителей, кириллица-двойник → латиница
///    (VLM часто путают «А» и «A» в номерах — это одна и та же метка).
/// 2. Пустой ответ — тоже голос: «не читается».
/// 3. Побеждает вариант с наибольшим суммарным весом; при равенстве — непустой, затем лексикографически.
/// 4. Согласие = вес победителя / суммарный вес всех учителей.
/// </summary>
public static class TextConsensus
{
    private static readonly Dictionary<char, char> Homoglyphs = new()
    {
        ['А'] = 'A', ['В'] = 'B', ['Е'] = 'E', ['К'] = 'K', ['М'] = 'M', ['Н'] = 'H',
        ['О'] = 'O', ['Р'] = 'P', ['С'] = 'C', ['Т'] = 'T', ['У'] = 'Y', ['Х'] = 'X',
    };

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var raw in text.ToUpperInvariant())
        {
            if (char.IsWhiteSpace(raw) || raw is '-' or '_' or '|' or '.' or ',') continue;
            sb.Append(Homoglyphs.TryGetValue(raw, out var latin) ? latin : raw);
        }
        return sb.ToString();
    }

    public static ConsensusResult Compute(IReadOnlyList<TeacherVote> votes)
    {
        if (votes.Count == 0) throw new DomainException("At least one teacher vote is required");
        if (votes.Any(v => v.Weight <= 0)) throw new DomainException("Teacher weights must be positive");
        var totalWeight = votes.Sum(v => v.Weight);

        var winner = votes
            .GroupBy(v => Normalize(v.Text))
            .Select(g => (Text: g.Key, Weight: g.Sum(v => v.Weight)))
            .OrderByDescending(g => g.Weight)
            .ThenByDescending(g => g.Text.Length > 0)
            .ThenBy(g => g.Text, StringComparer.Ordinal)
            .First();

        return new ConsensusResult([], winner.Text.Length == 0 ? null : winner.Text, Math.Round(winner.Weight / totalWeight, 4));
    }
}
