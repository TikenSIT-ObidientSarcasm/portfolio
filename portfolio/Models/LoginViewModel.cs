using System.ComponentModel.DataAnnotations;

namespace portfolio.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    // Where to send the user after logging in
    public string? ReturnUrl { get; set; }
}