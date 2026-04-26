namespace HardwareStore.Core.Interfaces;

public interface IEmailService
{
    Task SendSignupNotificationAsync(string userEmail, string userName);
    Task SendApprovalNotificationAsync(string userEmail, string userName);
    Task SendRejectionNotificationAsync(string userEmail, string userName);
}
