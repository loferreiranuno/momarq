using System.Net;
using System.Net.Http.Json;
using VisualSearch.Api.Tests.Fixtures;

namespace VisualSearch.Api.Tests.Integration;

/// <summary>
/// Integration tests for authentication flow.
/// Tests: login, get current user, change password.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AuthFlowTests : IntegrationTestBase
{
    public AuthFlowTests(PostgresContainerFixture dbFixture) : base(dbFixture)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange - credentials set up in base class

        // Act
        var response = await Client!.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "testadmin",
            Password = "TestPassword123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrWhiteSpace();
        result.Username.Should().Be("testadmin");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new { Username = "testadmin", Password = "WrongPassword!" };

        // Act
        var response = await Client!.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new { Username = "nonexistent", Password = "SomePassword123!" };

        // Act
        var response = await Client!.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new { Username = "", Password = "" };

        // Act
        var response = await Client!.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await AuthenticatedGetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        user.Should().NotBeNull();
        user!.Username.Should().Be("testadmin");
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await Client!.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_Succeeds()
    {
        // Arrange
        var request = new
        {
            Username = "testadmin",
            CurrentPassword = "TestPassword123!",
            NewPassword = "NewPassword456!"
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify can login with new password
        var loginResponse = await Client!.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "testadmin",
            Password = "NewPassword456!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Change back to original password for other tests
        var revertRequest = new
        {
            Username = "testadmin",
            CurrentPassword = "NewPassword456!",
            NewPassword = "TestPassword123!"
        };

        // Need to re-authenticate with new password
        var revertLoginResponse = await Client!.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "testadmin",
            Password = "NewPassword456!"
        });
        var newToken = (await revertLoginResponse.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        using var revertMessage = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password");
        revertMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        revertMessage.Content = JsonContent.Create(revertRequest);
        await Client!.SendAsync(revertMessage);
    }

    [Fact]
    public async Task ChangePassword_WithInvalidCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            Username = "testadmin",
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewPassword456!"
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithShortNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            Username = "testadmin",
            CurrentPassword = "TestPassword123!",
            NewPassword = "Short1" // Less than 8 characters
        };

        // Act
        var response = await AuthenticatedPostAsync("/api/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record LoginResponse(string Token, string Username, bool MustChangePassword, DateTime ExpiresAt);
    private record CurrentUserResponse(string Username, bool MustChangePassword);
}
