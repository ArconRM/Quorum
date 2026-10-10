using MediatR;

namespace Labeling.Application.Features.Teachers.RegisterTeacher;

public sealed record RegisterTeacherCommand(string Name, string Kind, string Task, double Weight) : IRequest<Guid>;
