using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HMS.Infrastructure.Persistence;
using HMS.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Testcontainers.MySql;

namespace HMS.IntegrationTests;

/// <summary>
/// Real MySQL so generated columns, unique indexes and row locks behave as in production.
/// Uses HMS_TEST_CONNECTION (a server-level connection; a throwaway database is created and dropped)
/// when set, otherwise starts a MySQL container (requires Docker).
/// </summary>
public sealed class HmsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo@1234";
    public const string AdminPassword = "Admin@1234";

    private MySqlContainer? _container;
    private string _connectionString = "";
    private string? _externalDatabase;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("HMS_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _externalDatabase = $"hms_test_{Guid.NewGuid():N}"[..24];
            _connectionString = new MySqlConnectionStringBuilder(external) { Database = _externalDatabase }.ConnectionString;
        }
        else
        {
            _container = new MySqlBuilder("mysql:8.0").WithDatabase("hms_test").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads configuration while building (before ConfigureAppConfiguration overrides apply),
        // so settings are passed as environment variables, which are read when the builder is created.
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__HmsDb"] = _connectionString,
            ["Jwt__SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            ["Seed__MigrateOnStartup"] = "true",
            ["Seed__DemoData"] = "true",
            ["Seed__AdminPassword"] = AdminPassword,
            ["Seed__DemoUserPassword"] = DemoPassword,
            ["RateLimit__LoginPerMinute"] = "1000",
        };
        foreach (var (key, value) in settings) Environment.SetEnvironmentVariable(key, value);
        builder.UseEnvironment("Testing");
    }

    public new async Task DisposeAsync()
    {
        if (_externalDatabase is not null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<HmsDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    public async Task<HttpClient> ClientForAsync(string username, string? password = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username, password = password ?? (username == "admin" ? AdminPassword : DemoPassword) });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<HmsDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<HmsDbContext>());
    }

    /// <summary>Next date (from tomorrow, hospital time) that falls Monday-Saturday, when demo doctors work mornings.</summary>
    public static DateOnly NextWorkingDay(int skip = 0)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)).AddDays(1);
        var found = 0;
        while (true)
        {
            if (date.DayOfWeek != DayOfWeek.Sunday && found++ == skip) return date;
            date = date.AddDays(1);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<HmsApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(HmsApiFactory.Json);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, HmsApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, HmsApiFactory.Json);

    public static async Task<long> IdOf(this HttpResponseMessage response) => (await response.JsonAsync()).GetProperty("id").GetInt64();

    public static Task<long> DoctorIdAsync(this HmsApiFactory f, string username) =>
        f.WithDbAsync(db => db.Users.Where(u => u.Username == username).Select(u => u.DoctorId!.Value).SingleAsync());
}
