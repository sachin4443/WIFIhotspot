namespace NxtHotspot.Application.Common;

/// <summary>Base for expected, client-facing errors. The API maps StatusCode to the HTTP response.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public sealed class ValidationFailedException(string message) : AppException(message, 400);
public sealed class UnauthorizedAppException(string message) : AppException(message, 401);
public sealed class InsufficientBalanceException(string message) : AppException(message, 402);
public sealed class ForbiddenException(string message = "You do not have access to this resource.") : AppException(message, 403);
public sealed class NotFoundException(string message) : AppException(message, 404);
public sealed class ConflictException(string message) : AppException(message, 409);
public sealed class ServiceUnavailableException(string message) : AppException(message, 503);
