using System.ComponentModel.DataAnnotations;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

public record ChangePasswordDto([Required] string CurrentPassword, [Required] string NewPassword);
