namespace HardwareStore.Infrastructure.Services;
using System.Diagnostics.CodeAnalysis;
using HardwareStore.Core.Interfaces;
using MailKit.Net.Smtp;
using MimeKit;

[ExcludeFromCodeCoverage]
public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "HardwareStore";
    public string AdminEmail { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

[ExcludeFromCodeCoverage]
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(Microsoft.Extensions.Options.IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendSignupNotificationAsync(string userEmail, string userName)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress("Admin", _settings.AdminEmail));
        message.Subject = $"New user signup pending approval: {userName}";
        message.Body = new TextPart("html")
        {
            Text = $@"
<h2>New User Signup Request</h2>
<p>A new user has signed up and is pending your approval:</p>
<ul>
  <li><strong>Name:</strong> {userName}</li>
  <li><strong>Email:</strong> {userEmail}</li>
</ul>
<p>Please log in to the admin panel to approve or reject this request.</p>"
        };
        await SendAsync(message);
    }

    public async Task SendApprovalNotificationAsync(string userEmail, string userName)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(userName, userEmail));
        message.Subject = "Your HardwareStore account has been approved";
        message.Body = new TextPart("html")
        {
            Text = $@"
<h2>Account Approved</h2>
<p>Hi {userName},</p>
<p>Your HardwareStore account has been approved! You can now log in and start comparing prices.</p>"
        };
        await SendAsync(message);
    }

    public async Task SendRejectionNotificationAsync(string userEmail, string userName)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(userName, userEmail));
        message.Subject = "HardwareStore account request update";
        message.Body = new TextPart("html")
        {
            Text = $@"
<h2>Account Request Update</h2>
<p>Hi {userName},</p>
<p>Unfortunately your account request was not approved at this time. Please contact the administrator for more information.</p>"
        };
        await SendAsync(message);
    }

    private async Task SendAsync(MimeMessage message)
    {
        if (string.IsNullOrEmpty(_settings.SmtpHost)) return;
        
        using var client = new SmtpClient();
        var socketOptions = _settings.EnableSsl
            ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable
            : MailKit.Security.SecureSocketOptions.None;
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions);
        if (!string.IsNullOrEmpty(_settings.SmtpUser))
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
