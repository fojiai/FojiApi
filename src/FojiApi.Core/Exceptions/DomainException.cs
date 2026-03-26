namespace FojiApi.Core.Exceptions;

/// <summary>
/// A business rule was violated. Maps to 400 Bad Request.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>
/// A requested resource could not be found. Maps to 404 Not Found.
/// </summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>
/// The caller lacks permission for this action. Maps to 403 Forbidden.
/// </summary>
public class ForbiddenException(string message = "You do not have permission to perform this action.") : Exception(message);

/// <summary>
/// A uniqueness constraint was violated. Maps to 409 Conflict.
/// </summary>
public class ConflictException(string message) : Exception(message);
