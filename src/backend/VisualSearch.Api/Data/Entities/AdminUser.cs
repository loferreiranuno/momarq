namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents an administrator user for the system.
/// </summary>
public sealed class AdminUser
{
    /// <summary>
    /// Gets or sets the unique identifier for the admin user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Gets or sets the BCrypt-hashed password.
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets whether the user must change their password on next login.
    /// </summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Gets or sets when this user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this user last logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
