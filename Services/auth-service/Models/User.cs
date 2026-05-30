namespace Danetka.AuthService.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = "User";
    public int Tokens { get; set; } = 5;
    public string SubscriptionPlan { get; set; } = "Trial";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}