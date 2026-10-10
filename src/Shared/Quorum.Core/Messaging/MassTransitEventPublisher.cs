using MassTransit;
using Quorum.Application.Abstractions;

namespace Quorum.Core.Messaging;

internal sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class =>
        publishEndpoint.Publish(message, ct);
}
