using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;

namespace Cms.Studio.Core.Services;

/// <summary>SMTP settings (appsettings.json → "Smtp"). When Host is empty the sender is "not configured".</summary>
public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>Minimal SMTP mail sender used for comment verification codes.</summary>
public class EmailSender
{
    private readonly SmtpSettings _settings;

    public EmailSender(SmtpSettings settings)
    {
        _settings = settings;
    }

    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        if (!_settings.IsConfigured)
            return false;

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
        };

        var from = string.IsNullOrWhiteSpace(_settings.From) ? _settings.UserName : _settings.From;
        using var message = new MailMessage(from, to, subject, body) { IsBodyHtml = true };
        await client.SendMailAsync(message);
        return true;
    }
}

public record CodeIssueResult(bool Success, string Message, string? DevCode = null);

/// <summary>
/// Email verification codes for guest comments (masuit.blog-style flow):
/// request a code → it is e-mailed to the address → the code must be submitted together
/// with the comment. Codes live 24h and are single-use; re-send is throttled to 2 min.
/// In the Development environment, when SMTP is not configured, the code is echoed
/// back in the response so the flow stays testable.
/// </summary>
public class EmailVerificationService
{
    public const int CodeLifetimeHours = 24;
    public const int ResendCooldownSeconds = 120;
    /// <summary>Wrong guesses allowed per code before it is invalidated (6-digit codes are brute-forceable otherwise).</summary>
    public const int MaxAttemptsPerCode = 5;

    private readonly EmailSender _sender;
    private readonly IHostEnvironment _environment;
    private static readonly ConcurrentDictionary<string, (string Code, DateTime Expires)> Codes = new();
    private static readonly ConcurrentDictionary<string, DateTime> LastSent = new();
    private static readonly ConcurrentDictionary<string, int> FailedAttempts = new();

    public EmailVerificationService(EmailSender sender, IHostEnvironment environment)
    {
        _sender = sender;
        _environment = environment;
    }

    public async Task<CodeIssueResult> IssueAsync(string email, string siteName)
    {
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsEmail(email))
            return new CodeIssueResult(false, "Please enter a valid email address.");

        if (LastSent.TryGetValue(email, out var sentAt))
        {
            var wait = (int)Math.Max(0, ResendCooldownSeconds - (DateTime.UtcNow - sentAt).TotalSeconds);
            if (wait > 0)
                return new CodeIssueResult(false, $"Please wait {wait} seconds before requesting another code. Don't forget to check your spam folder.");
        }

        // Cryptographically secure 6-digit code (Random.Shared is predictable enough to matter).
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        Codes[email] = (code, DateTime.UtcNow.AddHours(CodeLifetimeHours));
        LastSent[email] = DateTime.UtcNow;
        FailedAttempts.TryRemove(email, out _);

        var subject = $"{siteName} — your comment verification code";
        var body = $"<p>Your verification code is: <strong style=\"color:#d33\">{code}</strong></p>" +
                   $"<p>It is valid for {CodeLifetimeHours} hours. If you did not request it, ignore this email.</p>";

        var sent = await _sender.SendAsync(email, subject, body);
        if (sent)
            return new CodeIssueResult(true, $"A verification code has been sent to {email}. It is valid for {CodeLifetimeHours} hours.");

        if (_environment.IsDevelopment())
            return new CodeIssueResult(true, $"SMTP is not configured (Development mode) — your code is: {code}", code);

        return new CodeIssueResult(false, "The mail service is not configured on this site, comments cannot be verified right now.");
    }

    /// <summary>Validates and consumes the code for the email address (single use).</summary>
    public bool TryConsume(string email, string code)
    {
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        code = (code ?? string.Empty).Trim();

        if (!Codes.TryGetValue(email, out var entry))
            return false;
        if (entry.Expires < DateTime.UtcNow)
        {
            Codes.TryRemove(email, out _);
            FailedAttempts.TryRemove(email, out _);
            return false;
        }
        if (!string.Equals(entry.Code, code, StringComparison.Ordinal))
        {
            // Too many wrong guesses → burn the code so it cannot be brute-forced.
            if (FailedAttempts.AddOrUpdate(email, 1, (_, v) => v + 1) >= MaxAttemptsPerCode)
            {
                Codes.TryRemove(email, out _);
                FailedAttempts.TryRemove(email, out _);
            }
            return false;
        }

        Codes.TryRemove(email, out _);
        FailedAttempts.TryRemove(email, out _);
        return true;
    }

    public static bool IsEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            // Invalid email format
            return false;
        }
        catch (ArgumentException)
        {
            // Empty or null email
            return false;
        }
    }
}
