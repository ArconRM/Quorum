namespace Quorum.Application.Abstractions;

/// <summary>
/// Публикация интеграционного сообщения. Реализация — MassTransit bus outbox:
/// сообщение пишется в outbox-таблицу в той же транзакции, что и агрегат,
/// и уходит в брокер только после коммита.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
