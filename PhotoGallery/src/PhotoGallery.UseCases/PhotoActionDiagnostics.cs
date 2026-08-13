using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace PhotoGallery.UseCases;

/// <summary>
/// Produces a deliberately privacy-bounded Action diagnostic line. The caller
/// supplies only fixed vocabulary fields; photo identifiers are one-way hashed
/// before they cross the provider's Dlog boundary.
/// </summary>
public static class PhotoActionDiagnostics
{
    private static readonly byte[] CorrelationSalt = RandomNumberGenerator.GetBytes(32);

    public static string Correlate(string? stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return "none";
        }

        try
        {
            var raw = Encoding.UTF8.GetBytes(stableId);
            var input = new byte[CorrelationSalt.Length + raw.Length];
            CorrelationSalt.CopyTo(input, 0);
            raw.CopyTo(input, CorrelationSalt.Length);
            var digest = SHA256.HashData(input);
            return Convert.ToHexString(digest.AsSpan(0, 6)).ToLowerInvariant();
        }
        catch (Exception)
        {
            return "unavailable";
        }
    }

    public static string Correlate(IReadOnlyCollection<string>? stableIds)
    {
        if (stableIds is null || stableIds.Count == 0)
        {
            return "none";
        }

        try
        {
            var ordered = stableIds.OrderBy(id => id, StringComparer.Ordinal);
            return $"set-{stableIds.Count}-{Correlate(string.Join("\u001f", ordered))}";
        }
        catch (Exception)
        {
            return "unavailable";
        }
    }

    public static string Format(
        string action,
        string correlation,
        string validation,
        string branch,
        string status,
        Stopwatch stopwatch,
        Exception? exception = null) =>
        $"action={action} correlation={correlation} validation={validation} " +
        $"branch={branch} status={status} durationMs={Math.Max(0, stopwatch.ElapsedMilliseconds)} " +
        $"exception={exception?.GetType().Name ?? "none"}";
}