using FluentValidation;
using Quorum.Application.Exceptions;
using Quorum.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Quorum.Core.Web;

/// <summary>Маппинг исключений в RFC 7807 ProblemDetails.</summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update, retry the request"),
            ExternalServiceException => (StatusCodes.Status503ServiceUnavailable, "Dependency unavailable"),
            _ => (0, string.Empty),
        };
        if (status == 0) return false;

        http.Response.StatusCode = status;
        var context = new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = { Title = title, Detail = exception.Message, Status = status },
        };
        if (exception is ValidationException validation)
        {
            context.ProblemDetails.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }
        return await problems.TryWriteAsync(context);
    }
}
