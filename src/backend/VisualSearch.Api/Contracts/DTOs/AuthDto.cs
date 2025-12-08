namespace VisualSearch.Api.Contracts.DTOs;

/// <summary>
/// Result of a successful login operation.
/// </summary>
public record LoginResultDto(
    string Token,
    string Username,
    bool MustChangePassword,
    DateTime ExpiresAt
);

/// <summary>
/// Result of changing a password.
/// </summary>
public record ChangePasswordResultDto(
    bool Success,
    string? ErrorMessage = null,
    string? Token = null,
    DateTime? ExpiresAt = null
);

/// <summary>
/// Current user information.
/// </summary>
public record CurrentUserDto(
    string Username,
    bool MustChangePassword
);
