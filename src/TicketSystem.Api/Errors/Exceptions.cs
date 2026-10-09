namespace Jehualli.TicketSystem.Api.Errors;

// Each exception maps to one HTTP status in ApiExceptionHandler.

public class NotFoundException(string resource, object id)
    : Exception($"{resource} not found with id: {id}");

public class BadRequestException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

public class InvalidCredentialsException() : Exception("Invalid email or password");

public class ConflictException(string message) : Exception(message);
