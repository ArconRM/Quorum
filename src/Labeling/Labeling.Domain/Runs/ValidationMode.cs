namespace Labeling.Domain.Runs;

public enum ValidationMode
{
    /// <summary>Каждую метку проверяет человек; консенсус — только подсказка.</summary>
    Manual,

    /// <summary>Решает только консенсус: согласие ≥ порога — принять, иначе отбросить.</summary>
    Auto,

    /// <summary>Согласие ≥ порога — принять, иначе — человеку на проверку.</summary>
    Hybrid,
}
