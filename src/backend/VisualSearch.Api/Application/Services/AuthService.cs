using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for authentication and authorization.
/// </summary>
public sealed class AuthService
{
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;

    public AuthService(
        IAdminUserRepository adminUserRepository,
        JwtOptions jwtOptions,
        ILogger<AuthService> logger,
        IPasswordHasher<AdminUser> passwordHasher)
    {
        _adminUserRepository = adminUserRepository;
        _jwtOptions = jwtOptions;
        _logger = logger;
        _passwordHasher = passwordHasher;
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
        if (user is null)
        {
            _logger.LogWarning("Failed login attempt for username: {Username} (User not found)", username);
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Failed login attempt for username: {Username} (Invalid password)", username);
            return null;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _adminUserRepository.UpdateAsync(user, cancellationToken);
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

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new ChangePasswordResultDto(false, "Current password is incorrect");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await _adminUserRepository.UpdateAsync(user, cancellationToken);

        // Generate a new token with mustChangePassword = false
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var token = GenerateJwtToken(user.Id, user.Username, false, expiresAt);

        _logger.LogInformation("User {Username} changed their password", username);

        return new ChangePasswordResultDto(true, null, token, expiresAt);
    }

    /// <summary>
    /// Gets all admin users.
    /// </summary>
    public async Task<IEnumerable<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _adminUserRepository.GetAllAsync(cancellationToken);
        return users.Select(u => new AdminUserDto(u.Id, u.Username, u.CreatedAt, u.LastLoginAt));
    }

    /// <summary>
    /// Creates a new admin user.
    /// </summary>
    public async Task<AdminUserDto> CreateUserAsync(CreateAdminUserDto dto, CancellationToken cancellationToken = default)
    {
        var existingUser = await _adminUserRepository.GetByUsernameAsync(dto.Username, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"User with username '{dto.Username}' already exists.");
        }

        var user = new AdminUser
        {
            Username = dto.Username,
            PasswordHash = "", // Will be set below
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        await _adminUserRepository.AddAsync(user, cancellationToken);
        await _adminUserRepository.SaveChangesAsync(cancellationToken);

        return new AdminUserDto(user.Id, user.Username, user.CreatedAt, user.LastLoginAt);
    }

    /// <summary>
    /// Deletes an admin user.
    /// </summary>
    public async Task DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _adminUserRepository.GetByIdAsync(id, cancellationToken);
        if (user != null)
        {
            await _adminUserRepository.DeleteAsync(user, cancellationToken);
            await _adminUserRepository.SaveChangesAsync(cancellationToken);
        }
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
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
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
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
