using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Self-bootstrapping game-over overlay.
/// Call GameOverScreen.ShowGameOver(...) from anywhere — the screen creates its
/// own Canvas + panel at runtime, so no prefab or scene setup is required.
/// Press any key (or click) to restart.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    private GameObject      _panel;
    private TextMeshProUGUI _scoreText;
    private TextMeshProUGUI _levelText;
    private TextMeshProUGUI _linesText;

    private bool _isActive = false;

    // ── Static entry point ───────────────────────────────────────────────────────

    /// <summary>
    /// Shows the game-over screen.  Creates it on demand the first time.
    /// Safe to call from anywhere (PieceController, etc.).
    /// </summary>
    public static void ShowGameOver(int score, int level, int linesCleared)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("GameOverScreen");
            go.AddComponent<GameOverScreen>();
            // Awake() fires synchronously, setting Instance + building UI
        }
        Instance.Show(score, level, linesCleared);
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Survives if the scene is reloaded

        BuildCanvas();
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!_isActive) return;

        // Any key OR mouse click restarts the game
        if (Input.anyKeyDown)
            Restart();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Show(int score, int level, int linesCleared)
    {
        _scoreText.text = $"SCORE   {score:N0}";
        _levelText.text = $"LEVEL   {level}";
        _linesText.text = $"LINES   {linesCleared}";
        _panel.SetActive(true);
        _isActive = true;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private void Restart()
    {
        _isActive = false;
        _panel.SetActive(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Builds a full-screen Screen Space – Overlay canvas with a dark panel and
    /// centered text labels.  All done in code — no prefabs required.
    /// </summary>
    private void BuildCanvas()
    {
        // ── Canvas ──
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Always on top

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // ── Dark background panel ──
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.88f);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // ── Text labels ──
        CreateLabel("GAME OVER",
            new Vector2(0f, 200f), 96f,
            new Color(1f, 0.18f, 0.18f), FontStyles.Bold);

        CreateLabel("────────────────────",
            new Vector2(0f, 130f), 28f,
            new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal);

        _scoreText = CreateLabel("SCORE   0",
            new Vector2(0f, 56f), 48f,
            Color.white, FontStyles.Bold);

        _levelText = CreateLabel("LEVEL   1",
            new Vector2(0f, -20f), 38f,
            new Color(0.5f, 0.85f, 1f), FontStyles.Normal);

        _linesText = CreateLabel("LINES   0",
            new Vector2(0f, -80f), 38f,
            new Color(0.5f, 0.85f, 1f), FontStyles.Normal);

        CreateLabel("────────────────────",
            new Vector2(0f, -148f), 28f,
            new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal);

        CreateLabel("PRESS ANY KEY TO PLAY AGAIN",
            new Vector2(0f, -210f), 28f,
            new Color(1f, 1f, 0f), FontStyles.Normal);
    }

    private TextMeshProUGUI CreateLabel(string text, Vector2 anchoredPos,
        float fontSize, Color color, FontStyles style)
    {
        GameObject obj = new GameObject(text.Length > 16 ? text.Substring(0, 16) : text);
        obj.transform.SetParent(_panel.transform, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin       = new Vector2(0.5f, 0.5f);
        rect.anchorMax       = new Vector2(0.5f, 0.5f);
        rect.pivot           = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta       = new Vector2(900f, 100f);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;

        return tmp;
    }
}
