using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Cycles.ListCycles;

public sealed record ListCyclesQuery(Guid? CameraId, int Take) : IRequest<IReadOnlyList<CycleDto>>;
