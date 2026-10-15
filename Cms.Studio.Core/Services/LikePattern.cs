namespace Cms.Studio.Core.Services;

/// <summary>
/// Helpers for building safe LIKE patterns from user input. Without escaping, <c>%</c> / <c>_</c>
/// in a search box act as wildcards — an attacker can force full-text scans with patterns like
/// <c>%%%%%</c> or silently broaden their matches.
/// </summary>
public static class LikePattern
{
    /// <summary>Escapes the LIKE metacharacters so the value is matched literally (escape char: backslash).</summary>
    public static string Escape(string value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}
