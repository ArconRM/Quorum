using MediatR;

namespace Training.Application.Features.Cycles.StartCycle;

/// <summary>Запуск цикла адаптации камеры. Mode/Threshold null — берутся из настроек.</summary>
public sealed record StartCycleCommand(Guid CameraId, bool Scheduled, string? ValidationMode, double? AgreementThreshold) : IRequest<Guid>;
