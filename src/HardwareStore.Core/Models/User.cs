namespace HardwareStore.Core.Models;

public enum UserRole { User, Admin }
public enum UserStatus { PendingApproval, Active, Suspended }

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.PendingApproval;
    public int DailySearchLimit { get; set; } = 10;
    public int SearchesUsedToday { get; set; } = 0;
    public DateTime SearchLimitResetDate { get; set; } = DateTime.UtcNow.Date;
    public List<string> AllowedRetailerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? FacebookId { get; set; }
    public string DocumentType { get; set; } = "user";
}
