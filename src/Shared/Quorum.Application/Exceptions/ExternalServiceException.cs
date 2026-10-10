namespace Quorum.Application.Exceptions;

/// <summary>
/// Внешняя зависимость (GPU-воркер, соседний сервис по gRPC) недоступна или ответила ошибкой.
/// Хендлеры саги ловят её и переводят шаг в «провален», чтобы запустились компенсации.
/// </summary>
public sealed class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);
