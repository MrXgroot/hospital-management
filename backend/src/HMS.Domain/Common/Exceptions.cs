namespace HMS.Domain.Common;

/// <summary>Base for all expected errors. Middleware maps these to ProblemDetails with <see cref="Code"/>.</summary>
public abstract class HmsException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public Dictionary<string, object?> Extensions { get; } = [];

    public HmsException With(string key, object? value)
    {
        Extensions[key] = value;
        return this;
    }
}

public sealed class NotFoundException(string entity, object id)
    : HmsException("NOT_FOUND", $"{entity} {id} was not found.");

public sealed class ConflictException(string code, string message) : HmsException(code, message);

public sealed class BusinessRuleException(string code, string message) : HmsException(code, message);

public sealed class ForbiddenException(string message = "You are not allowed to perform this action.")
    : HmsException("FORBIDDEN", message);

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : HmsException("VALIDATION_FAILED", "One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
