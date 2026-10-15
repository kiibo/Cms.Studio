using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Admin credential verification and password changes.
///
/// Password resolution order:
///   1. <c>AdminPasswordHash</c> in the Setting table (set from the admin settings page),
///   2. <c>AdminAccount:PasswordHash</c> in configuration,
///   3. <c>AdminAccount:Password</c> plaintext in configuration (legacy — generates a startup warning).
/// </summary>
public class AdminAccountService
{
    public const string PasswordHashKey = "AdminPasswordHash";

    private readonly IConfiguration _configuration;
    private readonly SettingService _settings;

    public AdminAccountService(IConfiguration configuration, SettingService settings)
    {
        _configuration = configuration;
        _settings = settings;
    }

    /// <summary>Administrator user name from configuration (default "admin").</summary>
    public string Username => _configuration["AdminAccount:Username"] ?? "admin";

    public async Task<bool> VerifyAsync(string? username, string? password) =>
        AdminPasswordHasher.FixedEquals((username ?? string.Empty).Trim(), Username) &&
        await VerifyPasswordAsync(password ?? string.Empty);

    public async Task<bool> VerifyPasswordAsync(string password)
    {
        var dbHash = await _settings.GetAsync(PasswordHashKey, string.Empty);
        if (!string.IsNullOrEmpty(dbHash))
            return AdminPasswordHasher.Verify(password, dbHash);

        var configHash = _configuration["AdminAccount:PasswordHash"] ?? string.Empty;
        if (!string.IsNullOrEmpty(configHash))
            return AdminPasswordHasher.Verify(password, configHash);

        var plain = _configuration["AdminAccount:Password"] ?? string.Empty;
        return AdminPasswordHasher.FixedEquals(password, plain);
    }

    /// <summary>Stores a PBKDF2 hash in the Setting table; it overrides the configured password.</summary>
    public Task ChangePasswordAsync(string newPassword) =>
        _settings.SetAsync(PasswordHashKey, AdminPasswordHasher.Hash(newPassword));

    /// <summary>True while the site still runs on the shipped placeholder password.</summary>
    public async Task<bool> IsUsingDefaultPasswordAsync()
    {
        var dbHash = await _settings.GetAsync(PasswordHashKey, string.Empty);
        if (!string.IsNullOrEmpty(dbHash))
            return false;

        var configHash = _configuration["AdminAccount:PasswordHash"] ?? string.Empty;
        if (!string.IsNullOrEmpty(configHash))
            return false;

        var plain = _configuration["AdminAccount:Password"] ?? string.Empty;
        return plain.Length == 0 || plain == "change-me";
    }
}
