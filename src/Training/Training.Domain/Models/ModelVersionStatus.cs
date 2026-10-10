namespace Training.Domain.Models;

public enum ModelVersionStatus
{
    /// <summary>Обучена, ждёт оценки.</summary>
    Candidate,

    /// <summary>Работает на камере (одна на камеру).</summary>
    Published,

    /// <summary>Оказалась не лучше текущей.</summary>
    Rejected,

    /// <summary>Была опубликована раньше — к ней можно откатиться.</summary>
    Archived,
}
