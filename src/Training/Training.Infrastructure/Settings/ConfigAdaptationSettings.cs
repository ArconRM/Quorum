using Microsoft.Extensions.Configuration;
using Training.Application.Abstractions;

namespace Training.Infrastructure.Settings;

/// <summary>Секция Adaptation; читается при каждом обращении — можно менять через переменные окружения без пересборки.</summary>
internal sealed class ConfigAdaptationSettings(IConfiguration config) : IAdaptationSettings
{
    public string DefaultValidationMode => config["Adaptation:DefaultValidationMode"] ?? "Hybrid";
    public double DefaultAgreementThreshold => config.GetValue("Adaptation:DefaultAgreementThreshold", 0.7);
    public int MinTrainingLabels => config.GetValue("Adaptation:MinTrainingLabels", 10);
    public int Epochs => config.GetValue("Adaptation:Epochs", 10);
    public double MinImprovement => config.GetValue("Adaptation:MinImprovement", 0.0);
    public int MinHoldoutFrames => config.GetValue("Adaptation:MinHoldoutFrames", 20);
}
