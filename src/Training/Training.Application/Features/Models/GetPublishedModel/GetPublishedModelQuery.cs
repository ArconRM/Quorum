using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Models.GetPublishedModel;

/// <summary>Какую модель сейчас запускать на камере. Через кеш Redis.</summary>
public sealed record GetPublishedModelQuery(Guid CameraId) : IRequest<ModelVersionDto?>;
