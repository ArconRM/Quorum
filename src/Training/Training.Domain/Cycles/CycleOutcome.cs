namespace Training.Domain.Cycles;

public enum CycleOutcome
{
    /// <summary>Кандидат лучше текущей модели — опубликован.</summary>
    Promoted,

    /// <summary>Кандидат не лучше — на камере осталась прежняя модель.</summary>
    KeptCurrent,
}
