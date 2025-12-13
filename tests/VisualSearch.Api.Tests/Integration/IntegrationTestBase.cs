using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Tests.Fixtures;

namespace VisualSearch.Api.Tests.Integration;

/// <summary>
/// Base class for integration tests that need authentication.
/// Provides helper methods for login and authenticated requests.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    protected readonly PostgresContainerFixture DbFixture;
    protected WebApplicationFixture? AppFactory;
    protected HttpClient? Client;

    private const string TestUsername = "testadmin";
    private const string TestPassword = "TestPassword123!";
    private string? _authToken;

    protected IntegrationTestBase(PostgresContainerFixture dbFixture)
    {
        DbFixture = dbFixture;
    }

    public virtual async Task InitializeAsync()
    {
        AppFactory = new WebApplicationFixture(DbFixture.ConnectionString);
        Client = AppFactory.CreateClient();

        // Seed test admin user
        await SeedTestUserAsync();
    }

    public virtual async Task DisposeAsync()
    {
        Client?.Dispose();
        if (AppFactory is not null)
        {
            await AppFactory.DisposeAsync();
        }
    }

    /// <summary>
    /// Seeds a test admin user into the database.
    /// </summary>
    private async Task SeedTestUserAsync()
    {
        using var scope = AppFactory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();

        // Check if user already exists
        var existingUser = dbContext.AdminUsers.FirstOrDefault(u => u.Username == TestUsername);
        if (existingUser is null)
        {
            var user = new AdminUser
            {
                Username = TestUsername,
                PasswordHash = string.Empty,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(user, TestPassword);

            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();
            return;
        }

        existingUser.MustChangePassword = false;
        existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, TestPassword);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Authenticates with the test user and stores the token.
    /// </summary>
    protected async Task<string> AuthenticateAsync()
    {
        if (_authToken is not null)
        {
            return _authToken;
        }

        var loginResponse = await Client!.PostAsJsonAsync("/api/auth/login", new
        {
            Username = TestUsername,
            Password = TestPassword
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        _authToken = loginResult!.Token;

        return _authToken;
    }

    /// <summary>
    /// Creates an authenticated HttpClient with bearer token.
    /// </summary>
    protected async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var token = await AuthenticateAsync();
        var client = AppFactory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Sends an authenticated GET request.
    /// </summary>
    protected async Task<HttpResponseMessage> AuthenticatedGetAsync(string url)
    {
        var token = await AuthenticateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Client!.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated POST request with JSON body.
    /// </summary>
    protected async Task<HttpResponseMessage> AuthenticatedPostAsync<T>(string url, T body)
    {
        var token = await AuthenticateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await Client!.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated PUT request with JSON body.
    /// </summary>
    protected async Task<HttpResponseMessage> AuthenticatedPutAsync<T>(string url, T body)
    {
        var token = await AuthenticateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await Client!.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated DELETE request.
    /// </summary>
    protected async Task<HttpResponseMessage> AuthenticatedDeleteAsync(string url)
    {
        var token = await AuthenticateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Client!.SendAsync(request);
    }

    /// <summary>
    /// Gets the VisualSearchDbContext for direct database access in tests.
    /// </summary>
    protected VisualSearchDbContext GetDbContext()
    {
        var scope = AppFactory!.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();
    }

    /// <summary>
    /// DTO for login response.
    /// </summary>
    protected record LoginResponseDto(string Token, string Username, bool MustChangePassword, DateTime ExpiresAt);
}
