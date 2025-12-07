using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for authentication and authorization.
/// </summary>
public sealed class AuthService
{
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IConfiguration _configuration;

    public AuthService(
        IAdminUserRepository adminUserRepository,
        IConfiguration configuration)
    {
        _adminUserRepository = adminUserRepository;
        _configuration = configuration;
    }

    public async Task<string?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var isValid = await _adminUserRepository.ValidateCredentialsAsync(username, password, cancellationToken);
        if (!isValid)
        {
            return null;
        }

        var user = await _adminUserRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return GenerateJwtToken(user.Id, user.Username);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _adminUserRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _adminUserRepository.UpdateAsync(user, cancellationToken);

        return true;
    }

    private string GenerateJwtToken(int userId, string username)
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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
