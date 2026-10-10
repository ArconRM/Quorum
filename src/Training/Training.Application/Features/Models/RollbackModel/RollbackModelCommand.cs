using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Models.RollbackModel;

/// <summary>Вернуть на камеру одну из ранее опубликованных (архивных) версий.</summary>
public sealed record RollbackModelCommand(Guid VersionId) : IRequest<ModelVersionDto>;
