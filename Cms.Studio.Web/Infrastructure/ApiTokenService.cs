using System.Security.Cryptography;
using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// API access token for machine clients (AI agents, automations) that post articles.
/// The token lives in the Setting table (key <c>ApiToken</c>), is auto-generated on first
/// use, can be viewed/regenerated on the admin settings page, and is compared in
/// constant time. Send it as <c>Authorization: Bearer &lt;token&gt;</c> or <c>X-Api-Key</c>.
/// </summary>
public class ApiTokenService
{
    public const string SettingKey = "ApiToken";

    private readonly SettingService _settings;

    public ApiTokenService(SettingService settings)
    {
        _settings = settings;
    }

    public async Task<string> GetOrCreateAsync()
    {
        var token = await _settings.GetAsync(SettingKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        token = Generate();
        await _settings.SetAsync(SettingKey, token);
        return token;
    }

    public async Task<bool> VerifyAsync(string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
            return false;

        var token = await _settings.GetAsync(SettingKey, string.Empty);
        if (string.IsNullOrEmpty(token))
            return false;

        return AdminPasswordHasher.FixedEquals(provided.Trim(), token);
    }

    public async Task<string> RegenerateAsync()
    {
        var token = Generate();
        await _settings.SetAsync(SettingKey, token);
        return token;
    }

    private static string Generate() =>
        "cms_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
