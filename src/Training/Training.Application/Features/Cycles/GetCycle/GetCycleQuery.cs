using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Cycles.GetCycle;

public sealed record GetCycleQuery(Guid CycleId) : IRequest<CycleDto>;
