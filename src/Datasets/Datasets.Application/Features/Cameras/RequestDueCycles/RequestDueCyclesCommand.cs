using MediatR;

namespace Datasets.Application.Features.Cameras.RequestDueCycles;

/// <summary>
/// Тик планировщика: для каждой активной камеры, у которой подошёл срок и накопилось достаточно новых кадров,
/// публикуется AdaptationRequested (Training запускает цикл). Возвращает число запрошенных циклов.
/// </summary>
public sealed record RequestDueCyclesCommand : IRequest<int>;
