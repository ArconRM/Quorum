namespace Labeling.Application.Messages;

/// <summary>
/// Внутреннее сообщение сервиса: прогнать учителей и посчитать консенсус.
/// Вынесено из обработки StartLabeling, чтобы долгая работа шла отдельным шагом через очередь.
/// </summary>
public sealed record ProcessLabelingRun(Guid RunId);
