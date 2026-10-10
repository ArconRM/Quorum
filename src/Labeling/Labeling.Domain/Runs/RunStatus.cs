namespace Labeling.Domain.Runs;

public enum RunStatus
{
    /// <summary>Учителя размечают кадры.</summary>
    Running,

    /// <summary>Консенсус посчитан, часть меток ждёт человека.</summary>
    Reviewing,

    Completed,
    Failed,

    /// <summary>Компенсация: цикл адаптации провалился позже, разметка не использована.</summary>
    Discarded,
}
