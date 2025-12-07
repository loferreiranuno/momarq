using System.ComponentModel.DataAnnotations;

namespace VisualSearch.Api.Contracts.Requests;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required] string NewPassword
);
