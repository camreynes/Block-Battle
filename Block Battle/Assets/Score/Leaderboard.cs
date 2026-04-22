using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persists a local top-10 leaderboard in PlayerPrefs using JSONUtility.
/// Stores rank-ordered entries: name, score, level, lines, date (epoch seconds).
///
/// Everything here is pure data + I/O — no Unity UI. GameOverScreen consumes this
/// to decide whether a fresh score qualifies, and to render the table.
/// </summary>
public static class Leaderboard
{
    public const int MaxEntries = 10;
    private const string PrefKey = "BB_Leaderboard_v1";

    [Serializable]
    public class Entry
    {
        public string name;
        public int    score;
        public int    level;
        public int    lines;
        public long   dateEpochSec;

        public Entry() { }
        public Entry(string name, int score, int level, int lines)
        {
            this.name         = string.IsNullOrWhiteSpace(name) ? "AAA" : name.Trim();
            this.score        = score;
            this.level        = level;
            this.lines        = lines;
            this.dateEpochSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // JsonUtility can't serialize a bare List<T>, so we wrap it.
    [Serializable]
    private class Wrapper { public List<Entry> entries = new List<Entry>(); }

    // ── Public API ───────────────────────────────────────────────────────────────

    public static List<Entry> Load()
    {
        string json = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();

        List<Entry> entries;
        try
        {
            Wrapper w = JsonUtility.FromJson<Wrapper>(json);
            entries = w?.entries ?? new List<Entry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Failed to parse saved data, resetting: {e.Message}");
            return new List<Entry>();
        }

        // Auto-scrub on every load: if the filter word list has grown since a
        // name was saved, that entry gets yanked here and the updated list is
        // re-persisted so subsequent loads are clean.
        int removed = ProfanityFilter.ScanAndPurge(entries);
        if (removed > 0)
        {
            Debug.Log($"[Leaderboard] Purged {removed} flagged entry/entries on load.");
            Save(entries); // re-persist the cleaned list (Save also sorts + trims)
        }

        return entries;
    }

    public static void Save(List<Entry> entries)
    {
        // Always store sorted + capped, regardless of caller hygiene.
        entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

        Wrapper w = new Wrapper { entries = entries };
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(w));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// True if <paramref name="score"/> would earn a spot on the top-10.
    /// </summary>
    public static bool Qualifies(int score)
    {
        if (score <= 0) return false;
        var list = Load();
        if (list.Count < MaxEntries) return true;
        return score > list[list.Count - 1].score;
    }

    /// <summary>
    /// Inserts a new entry, resorts, trims to top-10, persists.
    /// Returns the 1-based rank it landed at, or -1 if it didn't make the cut.
    /// </summary>
    public static int Submit(string name, int score, int level, int lines)
    {
        var list = Load();
        var entry = new Entry(name, score, level, lines);
        list.Add(entry);
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        Save(list);

        int rank = list.IndexOf(entry);
        return rank >= 0 ? rank + 1 : -1;
    }

    /// <summary>
    /// Wipe the board — handy for a debug button or reset menu later.
    /// </summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Force a profanity sweep of the saved board and re-persist. Returns the
    /// number of entries removed. Intended for admin / moderator use — e.g.,
    /// bind to a dev keybind or call after the banned-word list is expanded.
    /// Note: regular Load() also auto-purges; this is for explicit callers.
    /// </summary>
    public static int PurgeProfanity()
    {
        var list = Load(); // Load already scrubs; we just re-run to report a count
        int removed = ProfanityFilter.ScanAndPurge(list);
        if (removed > 0) Save(list);
        return removed;
    }
}
