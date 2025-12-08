namespace VisualSearch.Api.Contracts.DTOs;

/// <summary>
/// DTO for admin user details.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Username">The username.</param>
/// <param name="CreatedAt">When the user was created.</param>
/// <param name="LastLoginAt">When the user last logged in.</param>
public record AdminUserDto(
    int Id,
    string Username,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

/// <summary>
/// DTO for creating a new admin user.
/// </summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The password.</param>
public record CreateAdminUserDto(
    string Username,
    string Password
);
