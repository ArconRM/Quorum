namespace Training.Domain.Cycles;

public enum CycleTrigger
{
    /// <summary>Планировщик Datasets: подошёл срок и накопились кадры.</summary>
    Scheduled,

    /// <summary>Оператор запустил вручную.</summary>
    Manual,
}
