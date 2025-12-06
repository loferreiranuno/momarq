using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;

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
        VisualSearchDbContext dbContext,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Username and password are required" });
        }

        var user = await dbContext.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return Results.Unauthorized();
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var token = GenerateJwtToken(user, configuration);

        logger.LogInformation("User {Username} logged in successfully", request.Username);

        return Results.Ok(new LoginResponse
        {
            Token = token,
            Username = user.Username,
            MustChangePassword = user.MustChangePassword,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
    }

    private static async Task<IResult> HandleChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        VisualSearchDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new { error = "Current and new passwords are required" });
        }

        if (request.NewPassword.Length < 8)
        {
            return Results.BadRequest(new { error = "New password must be at least 8 characters" });
        }

        var username = user.FindFirst(ClaimTypes.Name)?.Value;

        if (username is null)
        {
            return Results.Unauthorized();
        }

        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (adminUser is null)
        {
            return Results.Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, adminUser.PasswordHash))
        {
            return Results.BadRequest(new { error = "Current password is incorrect" });
        }

        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        adminUser.MustChangePassword = false;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} changed their password", username);

        return Results.Ok(new { message = "Password changed successfully" });
    }

    private static IResult HandleGetCurrentUserAsync(ClaimsPrincipal user)
    {
        var username = user.FindFirst(ClaimTypes.Name)?.Value;
        var mustChangePassword = user.FindFirst("must_change_password")?.Value == "true";

        if (username is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserResponse
        {
            Username = username,
            MustChangePassword = mustChangePassword
        });
    }

    private static string GenerateJwtToken(AdminUser user, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "VisualSearch.Api";
        var jwtAudience = configuration["Jwt:Audience"] ?? "VisualSearch.Frontend";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
