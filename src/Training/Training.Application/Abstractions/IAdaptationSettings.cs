namespace Training.Application.Abstractions;

/// <summary>Настройки цикла (секция Adaptation в конфигурации).</summary>
public interface IAdaptationSettings
{
    /// <summary>Стратегия проверки разметки для циклов, запущенных планировщиком.</summary>
    string DefaultValidationMode { get; }
    double DefaultAgreementThreshold { get; }

    /// <summary>Меньше принятых меток — обучать бессмысленно, цикл проваливается с компенсациями.</summary>
    int MinTrainingLabels { get; }

    int Epochs { get; }

    /// <summary>На сколько кандидат должен превзойти текущую модель, чтобы его опубликовали.</summary>
    double MinImprovement { get; }
}
