using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the player's running score, current level, lines cleared,
/// most recent clear type, and combo streak.
/// Floats over the game world — no background panel.
/// </summary>
public class ScoreTracker : MonoBehaviour
{
    // ── TMP references ──────────────────────────────────────────────────────────
    private TextMeshPro _labelText;   // "SCORE"
    private TextMeshPro _scoreText;   // running total – punch-animated on update
    private TextMeshPro _levelText;   // "LEVEL 3"
    private TextMeshPro _linesText;   // "LINES 27"
    private TextMeshPro _clearText;   // e.g. "B2B TETRIS" – color-coded
    private TextMeshPro _comboText;   // "COMBO x3" – only when streak >= 2

    // ── State ───────────────────────────────────────────────────────────────────
    private int _displayedScore = 0;
    private Coroutine _punchCoroutine;

    // ── Layout ──────────────────────────────────────────────────────────────────
    // Y values are in the scoreTracker's LOCAL space (parent scaled ~1.83×).
    private const float TextScale = 0.08f;
    private const float YLabel    =  0.60f;
    private const float YScore    =  0.22f;
    private const float YLevel    = -0.12f;
    private const float YLines    = -0.42f;
    private const float YClear    = -0.72f;
    private const float YCombo    = -1.00f;

    // ── Initialization ──────────────────────────────────────────────────────────
    public void InitializeSelf(GameObject textPrefab)
    {
        // "SCORE" header
        _labelText = CreateText(textPrefab, "ScoreLabel",
            new Vector3(0f, YLabel, -1f), TextScale, 30f, Color.white);
        _labelText.text      = "<palette freq=0.6>SCORE</palette>";
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.fontStyle = FontStyles.Bold;

        // Score number — white, large, punch-scale on every clear
        _scoreText = CreateText(textPrefab, "ScoreValue",
            new Vector3(0f, YScore, -1f), TextScale, 48f, Color.white);
        _scoreText.text      = "0";
        _scoreText.alignment = TextAlignmentOptions.Center;
        _scoreText.fontStyle = FontStyles.Bold;

        // Level — light blue
        _levelText = CreateText(textPrefab, "LevelValue",
            new Vector3(0f, YLevel, -1f), TextScale, 26f, new Color(0.5f, 0.85f, 1f));
        _levelText.text      = "LEVEL 1";
        _levelText.alignment = TextAlignmentOptions.Center;
        _levelText.fontStyle = FontStyles.Bold;

        // Lines cleared — slightly dimmer blue
        _linesText = CreateText(textPrefab, "LinesValue",
            new Vector3(0f, YLines, -1f), TextScale, 22f, new Color(0.4f, 0.75f, 0.9f));
        _linesText.text      = "LINES 0";
        _linesText.alignment = TextAlignmentOptions.Center;

        // Clear-type label — sketchy, color-coded by clear type
        _clearText = CreateText(textPrefab, "ClearType",
            new Vector3(0f, YClear, -1f), TextScale, 24f, Color.white);
        _clearText.text      = "";
        _clearText.alignment = TextAlignmentOptions.Center;
        _clearText.fontStyle = FontStyles.Bold;

        // Combo counter — warm orange, visible only during active streak
        _comboText = CreateText(textPrefab, "ComboCount",
            new Vector3(0f, YCombo, -1f), TextScale, 20f, new Color(1f, 0.45f, 0f));
        _comboText.text      = "";
        _comboText.alignment = TextAlignmentOptions.Center;
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by BlockGrid after every line clear (or drop-point addition).
    /// comboStreak is 1 on the first consecutive clear — only display COMBO from streak >= 2.
    /// </summary>
    public void UpdateScore(int totalScore, string clearType, int comboStreak, int level, int linesCleared)
    {
        _displayedScore = totalScore;

        // Score number: update + punch
        _scoreText.text = _displayedScore.ToString("N0");
        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(PunchScale(_scoreText.transform, 1.4f, 0.22f));

        // Level and lines always kept in sync
        _levelText.text = $"LEVEL {level}";
        _linesText.text = $"LINES {linesCleared}";

        // Clear type: only show when an actual clear happened (not on drop-point updates)
        if (!string.IsNullOrEmpty(clearType))
        {
            _clearText.color = GetClearColor(clearType);
            _clearText.text  = $"<sketchy freq=3 amp=0.08 delay=0>{clearType}</sketchy>";
        }

        // Combo: only after the 2nd consecutive clear
        _comboText.text = comboStreak >= 2 ? $"COMBO x{comboStreak}" : "";
    }

    /// <summary>
    /// Called when a piece locks without clearing — hides dynamic labels but keeps level/lines.
    /// </summary>
    public void ClearComboDisplay(int level, int linesCleared)
    {
        _clearText.text = "";
        _comboText.text = "";
        _levelText.text = $"LEVEL {level}";
        _linesText.text = $"LINES {linesCleared}";
    }

    // ── Animations ──────────────────────────────────────────────────────────────

    private IEnumerator PunchScale(Transform target, float peakMult, float duration)
    {
        Vector3 normal = Vector3.one * TextScale;
        Vector3 peak   = Vector3.one * (TextScale * peakMult);
        float half = duration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(normal, peak, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(peak, normal, t / half);
            yield return null;
        }

        target.localScale = normal;
        _punchCoroutine = null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static Color GetClearColor(string clearType)
    {
        if (clearType.StartsWith("B2B"))   return new Color(0f,   1f,   1f);    // cyan   – back-to-back
        if (clearType.Contains("T-SPIN"))  return new Color(0.75f, 0.3f, 1f);  // purple – T-Spin
        if (clearType == "TETRIS")         return new Color(1f,   0.85f, 0f);   // gold   – Tetris
        if (clearType == "TRIPLE")         return new Color(1f,   0.5f,  0f);   // orange – Triple
        if (clearType == "DOUBLE")         return new Color(0.3f, 1f,   0.3f);  // green  – Double
        return Color.white;                                                       // white  – Single
    }

    private TextMeshPro CreateText(GameObject prefab, string objName,
        Vector3 localPos, float scale, float fontSize, Color color)
    {
        GameObject obj = Instantiate(prefab);
        obj.name = objName;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale    = Vector3.one * scale;

        TextMeshPro tmp = obj.GetComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.color    = color;
        return tmp;
    }
}
