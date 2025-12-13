using System.Security.Claims;
using VisualSearch.Api.Application.Services;
using VisualSearch.Api.Services;

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
            .AllowAnonymous()
            .Produces<ChangePasswordResponse>(200)
            .Produces(400)
            .Produces(401)
            .WithName("ChangePassword")
            .WithDescription("Changes the user's password. Validates via username and current password.");

        group.MapGet("/me", HandleGetCurrentUserAsync)
            .RequireAuthorization("Admin")
            .Produces<CurrentUserResponse>(200)
            .Produces(401)
            .WithName("GetCurrentUser")
            .WithDescription("Gets the current authenticated user's information.");

        group.MapPost("/sse-ticket", HandleCreateSseTicketAsync)
            .RequireAuthorization("Admin")
            .Produces<CreateSseTicketResponse>(200)
            .Produces(400)
            .Produces(401)
            .WithName("CreateSseTicket")
            .WithDescription("Issues a short-lived, one-time SSE ticket for EventSource authentication.");
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
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(
            request.Username,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(new ChangePasswordResponse
        {
            Success = true,
            Token = result.Token,
            ExpiresAt = result.ExpiresAt
        });
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

    private static IResult HandleCreateSseTicketAsync(
        CreateSseTicketRequest request,
        ClaimsPrincipal user,
        SseTicketService sseTicketService)
    {
        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.BadRequest(new { error = "Purpose is required" });
        }

        var subject = user.FindFirst(ClaimTypes.Name)?.Value;
        var issued = sseTicketService.Issue(request.Purpose.Trim(), subject);

        return Results.Ok(new CreateSseTicketResponse
        {
            Ticket = issued.Ticket,
            ExpiresAt = issued.ExpiresAt
        });
    }
}

public sealed record CreateSseTicketRequest
{
    public required string Purpose { get; init; }
}

public sealed record CreateSseTicketResponse
{
    public required string Ticket { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
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
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets the current password.</summary>
    public required string CurrentPassword { get; init; }

    /// <summary>Gets or sets the new password.</summary>
    public required string NewPassword { get; init; }
}

/// <summary>
/// Change password response DTO.
/// </summary>
public sealed record ChangePasswordResponse
{
    /// <summary>Gets or sets whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets or sets the new JWT token.</summary>
    public string? Token { get; init; }

    /// <summary>Gets or sets when the token expires.</summary>
    public DateTime? ExpiresAt { get; init; }
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
