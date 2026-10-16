using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HMS.Api.Infrastructure;
using HMS.Application;
using HMS.Application.Common;
using HMS.Infrastructure;
using HMS.Infrastructure.Persistence.Seed;
using HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddControllers(o =>
    {
        o.Filters.Add<ValidationFilter>();
        // Required-ness is FluentValidation's job; MVC would otherwise reject non-nullable strings first.
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationFilter.ModelStateProblem)
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new TimeOnlyHourMinuteConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Set it with `dotnet user-secrets`.");
if (builder.Environment.IsProduction() && jwt.SigningKey.StartsWith("DEV-ONLY", StringComparison.Ordinal))
    throw new InvalidOperationException("The development JWT signing key must not be used in production.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = HmsClaims.Username,
            RoleClaimType = HmsClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var loginPermitsPerMinute = builder.Configuration.GetValue("RateLimit:LoginPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
// Logs path, status and timing only. Request bodies contain patient data and are never logged.
app.UseSerilogRequestLogging(o =>
    o.EnrichDiagnosticContext = (diag, http) => diag.Set("UserId", http.User.FindFirst(HmsClaims.UserId)?.Value ?? "anonymous"));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "HMS API"));
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();

public partial class Program;
