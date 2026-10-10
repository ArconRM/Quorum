using MediatR;

namespace Identity.Application.Features.Auth.Register;

public sealed record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<Guid>;
