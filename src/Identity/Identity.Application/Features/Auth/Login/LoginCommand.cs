using Identity.Application.Abstractions;
using MediatR;

namespace Identity.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<AccessToken>;
