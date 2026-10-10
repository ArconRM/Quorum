namespace Datasets.Domain.Cameras;

/// <summary>Что модель делает на кадрах этой камеры.</summary>
public enum TaskType
{
    /// <summary>Детекция объектов (например, номерных пластин).</summary>
    Detection,

    /// <summary>Распознавание текста на кропах (например, номера).</summary>
    Ocr,
}
