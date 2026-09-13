using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace StoreApiLR11.Filters;

public class DatabaseExceptionFilter(ILogger<DatabaseExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateException dbException)
        {
            return;
        }

        logger.LogError(dbException, "Database update error");

        var innerMessage = dbException.InnerException?.Message.ToLowerInvariant();
        var (status, title, detail) = innerMessage switch
        {
            var message when message?.Contains("foreign key constraint fails") == true =>
                (StatusCodes.Status409Conflict, "Referential integrity violation", "Cannot modify record: it is referenced by other data."),
            var message when message?.Contains("duplicate entry") == true ||
                             message?.Contains("unique constraint") == true =>
                (StatusCodes.Status409Conflict, "Duplicate entry", "A record with the same unique value already exists."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Database error", "An error occurred while saving data.")
        };

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.HttpContext.Request.Path
        })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
