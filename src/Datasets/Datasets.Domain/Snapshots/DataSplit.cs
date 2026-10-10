namespace Datasets.Domain.Snapshots;

public enum DataSplit
{
    /// <summary>Обучение студента.</summary>
    Train,

    /// <summary>Отложенная выборка: сравнение кандидата с текущей моделью.</summary>
    Holdout,
}
