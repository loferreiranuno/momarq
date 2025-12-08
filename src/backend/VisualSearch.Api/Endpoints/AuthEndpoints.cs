using System.Security.Claims;
using VisualSearch.Api.Application.Services;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Authentication endpoints for admin users.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps the authentication endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", HandleLoginAsync)
            .Produces<LoginResponse>(200)
            .Produces(400)
            .Produces(401)
            .WithName("Login")
            .WithDescription("Authenticates an admin user and returns a JWT token.");

        group.MapPost("/change-password", HandleChangePasswordAsync)
            .RequireAuthorization("Admin")
            .Produces(200)
            .Produces(400)
            .Produces(401)
            .WithName("ChangePassword")
            .WithDescription("Changes the authenticated user's password.");

        group.MapGet("/me", HandleGetCurrentUserAsync)
            .RequireAuthorization("Admin")
            .Produces<CurrentUserResponse>(200)
            .Produces(401)
            .WithName("GetCurrentUser")
            .WithDescription("Gets the current authenticated user's information.");
    }

    private static async Task<IResult> HandleLoginAsync(
        LoginRequest request,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Username, request.Password, cancellationToken);

        if (result is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new LoginResponse
        {
            Token = result.Token,
            Username = result.Username,
            MustChangePassword = result.MustChangePassword,
            ExpiresAt = result.ExpiresAt
        });
    }

    private static async Task<IResult> HandleChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var username = user.FindFirst(ClaimTypes.Name)?.Value;
        if (username is null)
        {
            return Results.Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(
            username,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(new { message = "Password changed successfully" });
    }

    private static IResult HandleGetCurrentUserAsync(ClaimsPrincipal user)
    {
        var currentUser = AuthService.GetCurrentUser(user);

        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserResponse
        {
            Username = currentUser.Username,
            MustChangePassword = currentUser.MustChangePassword
        });
    }
}

/// <summary>
/// Login request DTO.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets the password.</summary>
    public required string Password { get; init; }
}

/// <summary>
/// Login response DTO.
/// </summary>
public sealed record LoginResponse
{
    /// <summary>Gets or sets the JWT token.</summary>
    public required string Token { get; init; }

    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets whether the user must change their password.</summary>
    public required bool MustChangePassword { get; init; }

    /// <summary>Gets or sets when the token expires.</summary>
    public required DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Change password request DTO.
/// </summary>
public sealed record ChangePasswordRequest
{
    /// <summary>Gets or sets the current password.</summary>
    public required string CurrentPassword { get; init; }

    /// <summary>Gets or sets the new password.</summary>
    public required string NewPassword { get; init; }
}

/// <summary>
/// Current user response DTO.
/// </summary>
public sealed record CurrentUserResponse
{
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets whether the user must change their password.</summary>
    public required bool MustChangePassword { get; init; }
}
