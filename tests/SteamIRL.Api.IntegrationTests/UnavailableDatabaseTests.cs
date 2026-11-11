using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class UnavailableDatabaseTests
{
    [Theory]
    [InlineData("/api/activities")]
    [InlineData("/api/locations")]
    [InlineData("/api/locations/aaaa1111-1111-4111-8111-111111111111")]
    [InlineData("/api/sessions")]
    [InlineData("/api/sessions/cccc1111-1111-4111-8111-111111111111")]
    public async Task Database_failure_keeps_liveness_but_fails_readiness_and_returns_safe_problem(string path)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:SkipDatabaseRoleCheck"] = "true",
                ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=steamirl_test_unavailable;Username=test;Timeout=1;Command Timeout=1"
            })));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
    }
}
