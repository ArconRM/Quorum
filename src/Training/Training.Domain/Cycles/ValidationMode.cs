namespace Training.Domain.Cycles;

/// <summary>Стратегия проверки разметки, с которой цикл запускает Labeling.</summary>
public enum ValidationMode
{
    Manual,
    Auto,
    Hybrid,
}
