using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Teachers.ListTeachers;

public sealed record ListTeachersQuery(string? Task) : IRequest<IReadOnlyList<TeacherDto>>;
