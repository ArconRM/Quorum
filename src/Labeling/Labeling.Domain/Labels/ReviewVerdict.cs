namespace Labeling.Domain.Labels;

public enum ReviewVerdict
{
    /// <summary>Подсказка консенсуса верна.</summary>
    Approve,

    /// <summary>Разметчик исправил метку.</summary>
    Correct,

    /// <summary>Кадр непригоден для обучения.</summary>
    Reject,
}
