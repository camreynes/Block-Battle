using System;
using UnityEngine;

/// <summary>
/// Persistent lifetime stats for Block Battle.
///
/// Two flavors of state live here:
///   1. Lifetime aggregates persisted to PlayerPrefs as JSON. These survive
///      app restarts and accumulate across every game ever played.
///   2. A short-lived "current run" tracker (StartRun → mid-run record calls
///      → EndRun) so we can fold the run's totals into the lifetime values
///      and track per-run records (highest score / level / combo / longest
///      survival time).
///
/// All public mutators auto-persist immediately. PlayerPrefs writes are cheap
/// for this size of data, and saving on every event means a crash mid-session
/// doesn't lose someone's tetris count.
///
/// Counter ordering (matches the Stats tab spec):
///   linesCleared, singles, doubles, triples, tetris,
///   miniTSpin, tSpinDouble, tSpinTriple, perfectClears,
///   highestScore, highestLevel, highestComboStreak, longestSurvivalSeconds,
///   totalGamesPlayed, totalMinutesPlayed, totalLevelsPassed
/// </summary>
public static class PlayerStats
{
    private const string PrefKey = "BB_PlayerStats_v1";

    /// <summary>
    /// Persisted lifetime aggregates. JsonUtility serializes only public
    /// instance fields, so everything here is public — but the type lives
    /// inside the static class so external code only ever talks to the
    /// PlayerStats API, never constructs a Data directly.
    /// </summary>
    [Serializable]
    public class Data
    {
        // Clear-type counts (lifetime).
        public int linesCleared;
        public int singles;
        public int doubles;
        public int triples;
        public int tetris;
        public int miniTSpin;
        public int tSpinSingle;   // not in the user-facing list but harmless to track
        public int tSpinDouble;
        public int tSpinTriple;
        public int perfectClears;

        // Per-run records.
        public int  highestScore;
        public int  highestLevel;
        public int  highestComboStreak;
        public int  longestSurvivalSeconds;

        // Lifetime totals.
        public int    totalGamesPlayed;
        public double totalMinutesPlayed;   // double so partial-minute precision survives across many short games
        public int    totalLevelsPassed;    // sum of (final level - 1) across every game
    }

    // ── In-memory cache ──────────────────────────────────────────────────────
    // Lazy-loaded the first time anything reads or writes. After that, we
    // hold the live Data instance in memory so reads are free and writes
    // just dirty + persist this single object.
    private static Data _cached;

    // ── Current run (transient, never persisted on its own) ──────────────────
    private static bool   _runActive;
    private static float  _runStartUnscaledTime;
    private static int    _runStartLevel = 1; // captured at StartRun for "levels passed" math at EndRun

    // ── Public API: lifetime data access ─────────────────────────────────────

    public static Data Get()
    {
        if (_cached != null) return _cached;

        string json = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json))
        {
            _cached = new Data();
            return _cached;
        }

        try
        {
            _cached = JsonUtility.FromJson<Data>(json) ?? new Data();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerStats] Failed to parse, resetting: {e.Message}");
            _cached = new Data();
        }
        return _cached;
    }

    public static void Save()
    {
        if (_cached == null) return;
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(_cached));
        PlayerPrefs.Save();
    }

    /// <summary>Wipe all stats — useful for a debug menu / "reset save" button.</summary>
    public static void Clear()
    {
        _cached = new Data();
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    // ── Run lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Call when a fresh game starts (after the scene loads, after the grid
    /// initializes). Captures the start time and the starting level so EndRun
    /// can compute survival time + levels-passed.
    /// </summary>
    public static void StartRun(int startingLevel = 1)
    {
        _runActive = true;
        // unscaledTime so a paused game doesn't inflate survival time.
        _runStartUnscaledTime = Time.unscaledTime;
        _runStartLevel = Mathf.Max(1, startingLevel);

        var d = Get();
        d.totalGamesPlayed += 1;
        Save();
    }

    /// <summary>
    /// Call from PieceController on top-out (right before ShowGameOver).
    /// Folds the run's records into the lifetime data and persists.
    /// </summary>
    public static void EndRun(int finalScore, int finalLevel, int peakComboStreak)
    {
        if (!_runActive) return;
        _runActive = false;

        var d = Get();

        // Survival time — compute in seconds first so we can update the
        // record (an int second count) and add a precise minutes-double to
        // the lifetime total.
        float seconds = Mathf.Max(0f, Time.unscaledTime - _runStartUnscaledTime);
        int   secondsInt = Mathf.RoundToInt(seconds);
        d.totalMinutesPlayed += seconds / 60.0;

        if (secondsInt > d.longestSurvivalSeconds) d.longestSurvivalSeconds = secondsInt;

        // Records. Final score / level / peak combo all use simple max.
        if (finalScore       > d.highestScore)        d.highestScore        = finalScore;
        if (finalLevel       > d.highestLevel)        d.highestLevel        = finalLevel;
        if (peakComboStreak  > d.highestComboStreak)  d.highestComboStreak  = peakComboStreak;

        // Levels passed across this run = finalLevel - startingLevel (clamp at 0).
        int passed = Mathf.Max(0, finalLevel - _runStartLevel);
        d.totalLevelsPassed += passed;

        Save();
    }

    // ── Per-event recorders (called from BlockGrid mid-run) ──────────────────

    /// <summary>
    /// Records a line clear by its descriptive type (the same string BlockGrid
    /// already builds for SFX routing). Strips an optional "B2B " prefix and
    /// also bumps `linesCleared` by the appropriate row count so callers don't
    /// have to track that themselves.
    /// </summary>
    public static void RecordClear(string clearType)
    {
        if (string.IsNullOrEmpty(clearType)) return;

        // Drop the B2B marker — back-to-back is a scoring concept, not a
        // distinct clear type, so it shouldn't double-count.
        string raw = clearType.StartsWith("B2B ") ? clearType.Substring(4) : clearType;

        var d = Get();
        switch (raw)
        {
            case "SINGLE":         d.singles      += 1; d.linesCleared += 1; break;
            case "DOUBLE":         d.doubles      += 1; d.linesCleared += 2; break;
            case "TRIPLE":         d.triples      += 1; d.linesCleared += 3; break;
            case "TETRIS":         d.tetris       += 1; d.linesCleared += 4; break;
            case "T-SPIN SINGLE":  d.tSpinSingle  += 1; d.linesCleared += 1; break;
            case "T-SPIN DOUBLE":  d.tSpinDouble  += 1; d.linesCleared += 2; break;
            case "T-SPIN TRIPLE":  d.tSpinTriple  += 1; d.linesCleared += 3; break;
            case "MINI T-SPIN":    d.miniTSpin    += 1; d.linesCleared += 1; break;

            // Unknown / non-clear types (empty string for soft drops, etc.)
            // fall through silently. Better than throwing on a typo.
            default: break;
        }
        Save();
    }

    /// <summary>Increments the perfect-clear counter (board fully empty after a clear).</summary>
    public static void RecordPerfectClear()
    {
        var d = Get();
        d.perfectClears += 1;
        Save();
    }

    // ── Convenience formatters (used by the Stats tab UI) ────────────────────

    /// <summary>"Hh Mm" / "Mm" / "0m" depending on size — concise for the panel.</summary>
    public static string FormatMinutes(double minutes)
    {
        if (minutes < 1) return "< 1m";
        int totalMinutes = (int)Math.Floor(minutes);
        int hours = totalMinutes / 60;
        int mins  = totalMinutes % 60;
        if (hours > 0) return $"{hours}h {mins}m";
        return $"{mins}m";
    }

    /// <summary>"Hh Mm Ss" / "Mm Ss" / "Ss" for survival time.</summary>
    public static string FormatSeconds(int totalSeconds)
    {
        if (totalSeconds < 60) return $"{totalSeconds}s";
        int hours = totalSeconds / 3600;
        int mins  = (totalSeconds % 3600) / 60;
        int secs  = totalSeconds % 60;
        if (hours > 0) return $"{hours}h {mins}m {secs}s";
        return $"{mins}m {secs}s";
    }
}
