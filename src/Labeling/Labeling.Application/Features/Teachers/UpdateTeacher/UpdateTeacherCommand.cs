using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Teachers.UpdateTeacher;

/// <summary>Изменения действуют на следующие прогоны: в текущем прогоне вес зафиксирован при старте.</summary>
public sealed record UpdateTeacherCommand(Guid TeacherId, double? Weight, bool? IsActive) : IRequest<TeacherDto>;
