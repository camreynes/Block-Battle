using System.Collections.Generic;
using System.Text;

/// <summary>
/// Lightweight local profanity filter.
///
/// Two purposes:
///   1. Autodetect — IsClean/Contains run at name-entry time so we reject bad
///      names before they hit the leaderboard.
///   2. Purge — ScanAndPurge sweeps already-saved leaderboard entries and
///      removes any that match. Call this on load so older saves can't carry
///      bad names forward past a filter upgrade.
///
/// Matching strategy:
///   - Lowercases and strips non-alphanumerics.
///   - Normalizes common leetspeak (0→o, 1→i, 3→e, 4→a, 5→s, 7→t, @→a, $→s, !→i).
///   - Collapses repeated characters ("fuuuuck" → "fuck") before matching.
///   - Does a substring search so embedded forms like "xxfuckxx" still trip.
///
/// This won't catch every creative spelling — nothing free of false positives will —
/// but it handles the common cases and is easy to extend by adding to _banned.
/// </summary>
public static class ProfanityFilter
{
    // Keep this list lowercase. Substrings, not whole words — "ass" will flag
    // "badass" too. Accept the false-positive tradeoff; the dataset is small.
    // Add or remove entries as needed.
    private static readonly string[] _banned =
    {
        "fuck", "shit", "bitch", "cunt", "asshole", "dick",
        "pussy", "bastard", "faggot", "nigger", "nigga",
        "retard", "whore", "slut", "cock", "piss",
        "twat", "wank", "jizz", "rape",
    };

    private static readonly Dictionary<char, char> _leet = new()
    {
        ['0'] = 'o', ['1'] = 'i', ['3'] = 'e', ['4'] = 'a',
        ['5'] = 's', ['7'] = 't', ['@'] = 'a', ['$'] = 's',
        ['!'] = 'i', ['|'] = 'i',
    };

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary> True if the input contains no flagged terms. </summary>
    public static bool IsClean(string input)
    {
        if (string.IsNullOrEmpty(input)) return true;
        string canonical = Canonicalize(input);
        foreach (var bad in _banned)
            if (canonical.Contains(bad)) return false;
        return true;
    }

    /// <summary>
    /// Returns the sanitized version — flagged substrings replaced with '*'.
    /// Useful when you'd rather censor than reject.
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Work on the original so casing/length are preserved as much as possible.
        // Match against a canonicalized mirror, mapping indices back one-to-one
        // would be fragile — instead, we rescan the original with a case-insensitive
        // contains for each banned word, and if the canonical check flags it,
        // we replace every letter of the original with '*'.
        if (IsClean(input)) return input;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input) sb.Append(char.IsLetterOrDigit(c) ? '*' : c);
        return sb.ToString();
    }

    /// <summary>
    /// Removes flagged entries from <paramref name="entries"/> in place.
    /// Returns the count removed.
    /// </summary>
    public static int ScanAndPurge(List<Leaderboard.Entry> entries)
    {
        if (entries == null) return 0;
        int before = entries.Count;
        entries.RemoveAll(e => e == null || !IsClean(e.name));
        return before - entries.Count;
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lowercase → strip non-alphanumerics → apply leet map → collapse runs of
    /// the same letter. "F.u.u.uuUck!" → "fuck".
    /// </summary>
    private static string Canonicalize(string input)
    {
        var sb = new StringBuilder(input.Length);
        char prev = '\0';

        foreach (char raw in input)
        {
            char c = char.ToLowerInvariant(raw);

            if (_leet.TryGetValue(c, out char mapped)) c = mapped;

            if (!char.IsLetter(c)) continue; // drop digits + punctuation after leet-mapping
            if (c == prev) continue;         // collapse runs

            sb.Append(c);
            prev = c;
        }

        return sb.ToString();
    }
}
