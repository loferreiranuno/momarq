using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for authentication and authorization.
/// </summary>
public sealed class AuthService
{
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAdminUserRepository adminUserRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _adminUserRepository = adminUserRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns login result with JWT token.
    /// </summary>
    public async Task<LoginResultDto?> LoginAsync(
        string username, 
        string password, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _adminUserRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", username);
            return null;
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _adminUserRepository.UpdateAsync(user, cancellationToken);

        // Generate JWT token
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var token = GenerateJwtToken(user.Id, user.Username, user.MustChangePassword, expiresAt);

        _logger.LogInformation("User {Username} logged in successfully", username);

        return new LoginResultDto(
            Token: token,
            Username: user.Username,
            MustChangePassword: user.MustChangePassword,
            ExpiresAt: expiresAt
        );
    }

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    public async Task<ChangePasswordResultDto> ChangePasswordAsync(
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return new ChangePasswordResultDto(false, "Current and new passwords are required");
        }

        if (newPassword.Length < 8)
        {
            return new ChangePasswordResultDto(false, "New password must be at least 8 characters");
        }

        var user = await _adminUserRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return new ChangePasswordResultDto(false, "User not found");
        }

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return new ChangePasswordResultDto(false, "Current password is incorrect");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        await _adminUserRepository.UpdateAsync(user, cancellationToken);

        // Generate a new token with mustChangePassword = false
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var token = GenerateJwtToken(user.Id, user.Username, false, expiresAt);

        _logger.LogInformation("User {Username} changed their password", username);

        return new ChangePasswordResultDto(true, null, token, expiresAt);
    }

    /// <summary>
    /// Gets current user information from claims.
    /// </summary>
    public static CurrentUserDto? GetCurrentUser(ClaimsPrincipal principal)
    {
        var username = principal.FindFirst(ClaimTypes.Name)?.Value;
        if (username is null)
        {
            return null;
        }

        var mustChangePassword = principal.FindFirst("must_change_password")?.Value == "true";

        return new CurrentUserDto(username, mustChangePassword);
    }

    private string GenerateJwtToken(int userId, string username, bool mustChangePassword, DateTime expiresAt)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "VisualSearchApi";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "VisualSearchClient";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("must_change_password", mustChangePassword.ToString().ToLowerInvariant()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
