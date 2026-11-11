using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SteamIRL.Api.Data;
using SteamIRL.Api.Features.Accounts;
using Xunit;

namespace SteamIRL.Api.IntegrationTests;

public sealed class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string Password = TestCredentials.Password;
    private HttpClient Browser() => factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
    private static string Email() => $"test-{Guid.NewGuid():N}@example.test";
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, HttpMethod method, object? body = null)
    {
        using var tokenResponse = await client.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> Register(HttpClient client, string email, string name = "Игрок") =>
        Send(client, "/api/auth/register", HttpMethod.Post, new { email, password = Password, displayName = name });

    [Fact]
    public async Task Registration_signs_in_hashes_password_and_exposes_only_public_profile_fields()
    {
        using var browser = Browser();
        var email = Email();
        using var response = await Register(browser, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("SteamIRL.Session=") && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var me = await browser.GetFromJsonAsync<MeResponse>("/api/me");
        Assert.Equal(email, me!.Email);
        using var guest = Browser();
        using var publicResponse = await guest.GetAsync($"/api/profiles/{me.Id}");
        using var profile = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "displayName", "id" }, profile.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("Игрок", profile.RootElement.GetProperty("displayName").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(user => user.Id == me.Id);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, Password));
    }

    [Fact]
    public async Task Duplicate_email_is_case_insensitive_and_does_not_create_second_account()
    {
        var email = Email();
        using var first = Browser(); using var second = Browser();
        Assert.Equal(HttpStatusCode.Created, (await Register(first, email)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Register(second, $" {email.ToUpperInvariant()} ")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(user => user.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Simultaneous_registration_creates_one_account()
    {
        var email = Email();
        using var first = Browser(); using var second = Browser();
        var responses = await Task.WhenAll(Register(first, email), Register(second, email));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_registration_returns_field_errors_without_creating_account()
    {
        using var browser = Browser();
        using var response = await Send(browser, "/api/auth/register", HttpMethod.Post,
            new { email = "invalid", password = "short", displayName = " " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Csrf_is_required_for_registration_login_profile_changes_and_logout()
    {
        using var browser = Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/auth/register", new { email = Email(), password = Password, displayName = "Игрок" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Register(browser, Email())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PatchAsJsonAsync("/api/me/profile", new { displayName = "Другой" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/api/auth/logout", null)).StatusCode);
        using var guest = Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/auth/login", new { email = Email(), password = Password })).StatusCode);
    }

    [Fact]
    public async Task Logout_clears_session_and_correct_login_restores_it()
    {
        var email = Email(); using var browser = Browser();
        await Register(browser, email);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(browser, "/api/auth/logout", HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(browser, "/api/auth/login", HttpMethod.Post, new { email, password = Password })).StatusCode);
        Assert.Equal(email, (await browser.GetFromJsonAsync<MeResponse>("/api/me"))!.Email);
    }

    [Fact]
    public async Task Invalid_credentials_do_not_reveal_whether_email_exists()
    {
        var email = Email(); using var owner = Browser(); await Register(owner, email);
        using var guest = Browser();
        using var known = await Send(guest, "/api/auth/login", HttpMethod.Post, new { email, password = "WrongPassword1" });
        using var unknown = await Send(guest, "/api/auth/login", HttpMethod.Post, new { email = Email(), password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        var a = await known.Content.ReadFromJsonAsync<JsonElement>(); var b = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Repeated_wrong_password_locks_account_temporarily()
    {
        var email = Email(); using var owner = Browser(); await Register(owner, email);
        using var guest = Browser();
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(guest, "/api/auth/login", HttpMethod.Post, new { email, password = "WrongPassword1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(guest, "/api/auth/login", HttpMethod.Post, new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task Profile_changes_are_private_to_owner_and_public_read_has_no_email()
    {
        using var owner = Browser(); using var other = Browser(); using var guest = Browser();
        var ownerResponse = await Register(owner, Email()); var me = (await ownerResponse.Content.ReadFromJsonAsync<MeResponse>())!;
        await Register(other, Email(), "Другой игрок");
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PatchAsJsonAsync("/api/me/profile", new { displayName = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Send(other, $"/api/profiles/{me.Id}", HttpMethod.Patch, new { displayName = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(other, "/api/me/profile", HttpMethod.Patch, new { userId = me.Id, displayName = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(owner, "/api/me/profile", HttpMethod.Patch, new { displayName = "  Новое имя  " })).StatusCode);
        var profile = await guest.GetFromJsonAsync<PublicProfileResponse>($"/api/profiles/{me.Id}");
        Assert.Equal("Новое имя", profile!.DisplayName);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, "/api/me/profile", HttpMethod.Patch, new { displayName = " " })).StatusCode);
    }

    [Fact]
    public async Task Authentication_request_burst_is_rate_limited()
    {
        using var browser = Browser();
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/auth/login", new { email = Email(), password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await browser.PostAsJsonAsync("/api/auth/login", new { email = Email(), password = Password })).StatusCode);
    }

    [Fact]
    public async Task Guest_session_is_explicitly_anonymous_and_unknown_profile_returns_404()
    {
        using var guest = Browser();
        using var session = JsonDocument.Parse(await guest.GetStringAsync("/api/auth/session"));
        Assert.Equal(JsonValueKind.Null, session.RootElement.GetProperty("user").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/profiles/{Guid.NewGuid()}")).StatusCode);
    }
}
