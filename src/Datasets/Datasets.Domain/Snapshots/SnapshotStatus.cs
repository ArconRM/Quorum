namespace Datasets.Domain.Snapshots;

public enum SnapshotStatus
{
    /// <summary>Идёт цикл адаптации, состав снимка зафиксирован.</summary>
    Locked,

    /// <summary>Цикл завершён, кадры использованы.</summary>
    Used,

    /// <summary>Цикл провалился, кадры возвращены в «новые».</summary>
    Discarded,
}
