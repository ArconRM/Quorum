namespace Training.Domain.Evaluations;

public enum EvaluationDecision
{
    Promote,
    Reject,

    /// <summary>Данных для сравнения недостаточно: отложенная выборка меньше минимума. Кандидат не публикуется.</summary>
    Inconclusive,
}
