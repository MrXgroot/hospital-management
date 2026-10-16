using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace HMS.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public long UserId => long.TryParse(Principal?.FindFirstValue(HmsClaims.UserId), out var id) ? id : 0;
    public Role Role => Enum.TryParse<Role>(Principal?.FindFirstValue(HmsClaims.Role), out var r) ? r : Role.Nurse;
    public long? DoctorId => long.TryParse(Principal?.FindFirstValue(HmsClaims.DoctorId), out var id) ? id : null;
}

/// <summary>Runs the FluentValidation validator (if any) for every action argument.</summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
                throw new RequestValidationException(result.Errors
                    .GroupBy(e => CamelCasePath(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
        }
        await next();
    }

    /// <summary>"Items[0].Dosage" → "items[0].dosage", matching the JSON the client sent.</summary>
    private static string CamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    /// <summary>Malformed JSON / unbindable values, in the same ProblemDetails shape as validation failures.</summary>
    public static IActionResult ModelStateProblem(ActionContext context)
    {
        var errors = context.ModelState.Where(e => e.Value?.Errors.Count > 0).ToDictionary(
            e => JsonNamingPolicy.CamelCase.ConvertName(e.Key.TrimStart('$', '.')),
            e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());
        var problem = new ProblemDetails { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest };
        problem.Extensions["code"] = "VALIDATION_FAILED";
        problem.Extensions["errors"] = errors;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}

/// <summary>Maps expected exceptions to RFC 7807 ProblemDetails with a stable `code`.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly HashSet<string> UnauthorizedCodes = ["INVALID_CREDENTIALS", "INVALID_REFRESH_TOKEN"];

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (HmsException ex)
        {
            var status = ex switch
            {
                RequestValidationException => StatusCodes.Status400BadRequest,
                NotFoundException => StatusCodes.Status404NotFound,
                ForbiddenException => StatusCodes.Status403Forbidden,
                ConflictException => StatusCodes.Status409Conflict,
                BusinessRuleException when UnauthorizedCodes.Contains(ex.Code) => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            var problem = new ProblemDetails { Title = ex.Message, Status = status, Type = $"https://hms/errors/{ex.Code.ToLowerInvariant().Replace('_', '-')}" };
            problem.Extensions["code"] = ex.Code;
            if (ex is RequestValidationException v) problem.Extensions["errors"] = v.Errors;
            foreach (var (key, value) in ex.Extensions) problem.Extensions[key] = value;
            await WriteAsync(context, problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            var problem = new ProblemDetails { Title = "This record was changed by someone else. Reload and try again.", Status = StatusCodes.Status409Conflict };
            problem.Extensions["code"] = "CONCURRENCY_CONFLICT";
            await WriteAsync(context, problem);
        }
        catch (Exception ex) when (!context.Response.HasStarted && ex is not OperationCanceledException)
        {
            // Log the exception type and trace only; messages from the DB can contain patient data.
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            var problem = new ProblemDetails { Title = "An unexpected error occurred.", Status = StatusCodes.Status500InternalServerError };
            problem.Extensions["code"] = "INTERNAL_ERROR";
            await WriteAsync(context, problem);
        }
    }

    private static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(), new TimeOnlyHourMinuteConverter() } };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}

/// <summary>Serialises TimeOnly as "HH:mm" (API contract); accepts "HH:mm" or "HH:mm:ss".</summary>
public sealed class TimeOnlyHourMinuteConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
}

public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
        return Task.CompletedTask;
    }
}

public static class Roles
{
    public const string Admin = nameof(Role.Admin);
    public const string AdminReception = "Admin,Receptionist";
    public const string AdminDoctor = "Admin,Doctor";
    public const string Clinical = "Admin,Doctor,Nurse";
    public const string ClinicalAndReception = "Admin,Receptionist,Doctor,Nurse";
    public const string DoctorOnly = nameof(Role.Doctor);
    public const string DoctorNurse = "Admin,Doctor,Nurse";
    public const string FrontDesk = "Admin,Receptionist,Nurse";
}
