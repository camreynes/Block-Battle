using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Self-bootstrapping game-over overlay.
/// Call GameOverScreen.ShowGameOver(...) from anywhere — the screen creates its
/// own Canvas + panels at runtime, so no prefab or scene setup is required.
///
/// Flow:
///   1. Stats panel shows on the left (GAME OVER, score/level/lines).
///   2. On the right: if the score does NOT qualify, show the leaderboard.
///      If it DOES, show an arcade-style letter picker IN PLACE OF the
///      leaderboard (initials entry via UP/DOWN/LEFT/RIGHT/ENTER).
///   3. After initials are submitted, the leaderboard replaces the picker
///      with the new entry highlighted.
///   4. Any key/click/gamepad button restarts.
///
/// Uses the new Input System (UnityEngine.InputSystem). Arcade picker reads
/// TetrixInputManager (joystick) plus Keyboard.current arrow keys (fallback).
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    // ── Stats panel ──
    private GameObject      _statsPanel;
    private TextMeshProUGUI _scoreText;
    private TextMeshProUGUI _levelText;
    private TextMeshProUGUI _linesText;
    private TextMeshProUGUI _hintText;

    // ── Right-side slot: leaderboard OR arcade entry ──
    private GameObject _leaderboardPanel;
    private readonly List<TextMeshProUGUI> _lbRankTexts  = new();
    private readonly List<TextMeshProUGUI> _lbNameTexts  = new();
    private readonly List<TextMeshProUGUI> _lbScoreTexts = new();
    private readonly List<Image>           _lbRowBgs     = new();

    private GameObject _arcadeEntryPanel;
    private readonly List<TextMeshProUGUI> _slotLetters = new();
    private readonly List<Image>           _slotBoxes   = new();

    // ── Arcade entry state ──
    private const int NameLength = 3;
    // Character set for the picker: A-Z only. Arcade convention — no space,
    // no backspace; player corrects mistakes by cycling past the letter.
    private static readonly char[] CharSet = BuildCharSet();

    private readonly int[] _slotCharIdx = new int[NameLength];
    private int  _cursorSlot            = 0;
    private bool _isAwaitingNameEntry   = false;

    // DAS for held UP/DOWN/LEFT/RIGHT in the picker.
    private float _heldRepeatTimer = 0f;
    private const float HeldInitialDelay = 0.35f;
    private const float HeldRepeatRate   = 0.08f;

    // ── Dim overlays (full-screen blackout layers behind the panels) ──
    // Stored so HideAll() can hide them on Restart — otherwise the dim layers
    // (created as direct children of the canvas) survive DontDestroyOnLoad
    // and continue to cover the freshly reloaded scene.
    private GameObject _dimBackground;
    private GameObject _dimTint;

    // ── State ──
    private bool _isActive      = false;
    private int  _pendingScore, _pendingLevel, _pendingLines;
    private int  _newEntryRank = -1;

    // Grace period after Show() before we accept "any key → restart".
    // Without this the death-frame key (e.g. hard-drop) is still
    // wasPressedThisFrame==true on the same Update() tick that Show() sets
    // _isActive=true, so the screen restarts instantly. 0.25s lets that
    // edge event flush before we start listening.
    private float _inputReadyAt = 0f;
    private const float InputGraceSeconds = 0.25f;

    private const int QueryPlayerId = 0; // single-player for now; multiplayer would plumb this through

    // ── Static entry point ───────────────────────────────────────────────────────

    public static void ShowGameOver(int score, int level, int linesCleared)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("GameOverScreen");
            go.AddComponent<GameOverScreen>();
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
        DontDestroyOnLoad(gameObject);

        EnsureEventSystem();
        BuildCanvas();
        HideAll();
    }

    private void Update()
    {
        if (!_isActive) return;

        if (_isAwaitingNameEntry)
        {
            HandleArcadeEntryInput();
            return;
        }

        // Swallow any input during the grace window so the key that *caused*
        // the game over doesn't immediately trigger a restart.
        if (Time.unscaledTime < _inputReadyAt) return;

        if (AnyKeyOrClickPressed())
            Restart();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Show(int score, int level, int linesCleared)
    {
        _pendingScore = score;
        _pendingLevel = level;
        _pendingLines = linesCleared;
        _newEntryRank = -1;

        _scoreText.text = $"SCORE   {score:N0}";
        _levelText.text = $"LEVEL   {level}";
        _linesText.text = $"LINES   {linesCleared}";

        // Re-show the dim overlays in case Restart() (or HideAll()) had hidden
        // them. Show() is also called the first time, when they're already active.
        if (_dimBackground != null) _dimBackground.SetActive(true);
        if (_dimTint       != null) _dimTint.SetActive(true);

        _statsPanel.SetActive(true);

        if (Leaderboard.Qualifies(score))
        {
            // Arcade entry instead of the leaderboard.
            ResetArcadeEntry();
            _leaderboardPanel.SetActive(false);
            _arcadeEntryPanel.SetActive(true);
            _isAwaitingNameEntry = true;
            _hintText.text = "NEW HIGH SCORE — ENTER YOUR INITIALS";
        }
        else
        {
            _arcadeEntryPanel.SetActive(false);
            _leaderboardPanel.SetActive(true);
            _isAwaitingNameEntry = false;
            _hintText.text = "PRESS ANY KEY TO PLAY AGAIN";
            RenderLeaderboard(highlightRank: -1);
        }

        _isActive     = true;
        _inputReadyAt = Time.unscaledTime + InputGraceSeconds;
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    private void HideAll()
    {
        _statsPanel.SetActive(false);
        _leaderboardPanel.SetActive(false);
        _arcadeEntryPanel.SetActive(false);

        // The two dim overlays are direct children of the canvas, not of any
        // panel. We need to hide them explicitly; otherwise they persist after
        // a scene reload (this script lives across scene loads via
        // DontDestroyOnLoad) and the player sees a black screen.
        if (_dimBackground != null) _dimBackground.SetActive(false);
        if (_dimTint       != null) _dimTint.SetActive(false);
    }

    private void Restart()
    {
        _isActive = false;
        HideAll();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Input-System equivalent of the old Input.anyKeyDown.
    /// Returns true on any key *this frame* (edge-triggered), plus mouse clicks.
    /// </summary>
    private static bool AnyKeyOrClickPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                              mouse.rightButton.wasPressedThisFrame ||
                              mouse.middleButton.wasPressedThisFrame)) return true;

        var gp = Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame ||
                           gp.buttonNorth.wasPressedThisFrame ||
                           gp.buttonEast.wasPressedThisFrame  ||
                           gp.buttonWest.wasPressedThisFrame  ||
                           gp.startButton.wasPressedThisFrame)) return true;

        return false;
    }

    // ── Arcade name entry ────────────────────────────────────────────────────────

    private static char[] BuildCharSet()
    {
        // A..Z only. Cycles wrap (Z+1 → A, A-1 → Z).
        var set = new char[26];
        for (int i = 0; i < 26; i++) set[i] = (char)('A' + i);
        return set;
    }

    private void ResetArcadeEntry()
    {
        for (int i = 0; i < NameLength; i++) _slotCharIdx[i] = 0; // all 'A'
        _cursorSlot = 0;
        _heldRepeatTimer = 0f;
        RenderArcadeEntry();
    }

    private void HandleArcadeEntryInput()
    {
        // --- Directional: UP/DOWN cycle letter, LEFT/RIGHT move cursor ---
        // Joystick-only: UP/DOWN/LEFT/RIGHT are bound to Stick/* in TetrixControls.
        // Keyboard players confirm with ENTER (which IS bound to <Keyboard>/enter).
        int dx = 0, dy = 0;
        bool edgeEvent = false;

        // Direction convention: DOWN steps forward through the alphabet
        // (A → B → … → Z → wrap to A), UP steps backward. Feels natural
        // because the arrow hint ▼ sits below the letter — pressing DOWN
        // "lowers" the letter toward Z.
        if (WasPressed(GameInputAction.UP))    { dy = -1; edgeEvent = true; }
        if (WasPressed(GameInputAction.DOWN))  { dy = +1; edgeEvent = true; }
        if (WasPressed(GameInputAction.LEFT))  { dx = -1; edgeEvent = true; }
        if (WasPressed(GameInputAction.RIGHT)) { dx = +1; edgeEvent = true; }

        // DAS/auto-repeat when the player holds a direction.
        if (!edgeEvent)
        {
            int hx = 0, hy = 0;
            if (IsHeld(GameInputAction.UP))    hy = -1;
            if (IsHeld(GameInputAction.DOWN))  hy = +1;
            if (IsHeld(GameInputAction.LEFT))  hx = -1;
            if (IsHeld(GameInputAction.RIGHT)) hx = +1;

            if (hx != 0 || hy != 0)
            {
                _heldRepeatTimer -= Time.unscaledDeltaTime;
                if (_heldRepeatTimer <= 0f)
                {
                    dx = hx; dy = hy;
                    _heldRepeatTimer = HeldRepeatRate;
                }
            }
            else
            {
                _heldRepeatTimer = HeldInitialDelay; // fresh delay next time a direction starts
            }
        }
        else
        {
            _heldRepeatTimer = HeldInitialDelay; // reset DAS on any edge press
        }

        if (dy != 0) CycleLetter(dy);
        if (dx != 0) MoveCursor(dx);

        // --- ENTER / HardDrop: commit current slot, advance, submit at end ---
        if (WasPressed(GameInputAction.ENTER) ||
            WasPressed(GameInputAction.HARD_DROP))
        {
            OnConfirm();
        }
    }

    private void CycleLetter(int direction)
    {
        int idx = _slotCharIdx[_cursorSlot];
        idx = (idx + direction) % CharSet.Length;
        if (idx < 0) idx += CharSet.Length;
        _slotCharIdx[_cursorSlot] = idx;
        RenderArcadeEntry();
    }

    private void MoveCursor(int direction)
    {
        _cursorSlot = Mathf.Clamp(_cursorSlot + direction, 0, NameLength - 1);
        RenderArcadeEntry();
    }

    private void OnConfirm()
    {
        // Not the last slot? Advance.
        if (_cursorSlot < NameLength - 1)
        {
            _cursorSlot++;
            RenderArcadeEntry();
            return;
        }

        // Last slot: attempt to submit.
        SubmitArcadeName();
    }

    private void SubmitArcadeName()
    {
        // Build the name from the selected characters. CharSet is A-Z only,
        // so the string is always 3 letters and never empty.
        var chars = new char[NameLength];
        for (int i = 0; i < NameLength; i++) chars[i] = CharSet[_slotCharIdx[i]];
        string name = new string(chars);

        // Profanity gate — flash the slots red and refuse the submit.
        if (!ProfanityFilter.IsClean(name))
        {
            FlashSlotsRejected();
            return;
        }

        _newEntryRank = Leaderboard.Submit(name, _pendingScore, _pendingLevel, _pendingLines);
        _isAwaitingNameEntry = false;
        _arcadeEntryPanel.SetActive(false);
        _leaderboardPanel.SetActive(true);
        _hintText.text = "PRESS ANY KEY TO PLAY AGAIN";
        RenderLeaderboard(highlightRank: _newEntryRank);
    }

    private void FlashSlotsRejected()
    {
        // Quick visual feedback: tint slot boxes red for a moment.
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        Color original = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        Color red      = new Color(0.6f, 0.1f, 0.1f, 0.95f);
        for (int i = 0; i < _slotBoxes.Count; i++) _slotBoxes[i].color = red;
        yield return new WaitForSecondsRealtime(0.35f);
        for (int i = 0; i < _slotBoxes.Count; i++) _slotBoxes[i].color = original;
    }

    private void RenderArcadeEntry()
    {
        for (int i = 0; i < NameLength; i++)
        {
            char c = CharSet[_slotCharIdx[i]];
            _slotLetters[i].text = c.ToString();

            bool active = (i == _cursorSlot);
            _slotBoxes[i].color = active
                ? new Color(0.2f, 0.25f, 0.4f, 0.95f)
                : new Color(0.10f, 0.10f, 0.15f, 0.95f);

            _slotLetters[i].color = active
                ? new Color(1f, 0.95f, 0.2f)
                : Color.white;
        }
    }

    // Input adapters — all routed through TetrixInputManager so joystick + any
    // keyboard bindings defined in TetrixControls.inputactions both work.
    private static bool WasPressed(GameInputAction a) => TetrixInputManager.WasPressed(a, QueryPlayerId);
    private static bool IsHeld(GameInputAction a)     => TetrixInputManager.IsHeld(a,     QueryPlayerId);

    // ── UI construction ──────────────────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(es);
    }

    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Much darker overlay than before — near-opaque to fully blot out the
        // gameplay grid behind. Two stacked layers for a subtle navy tint.
        _dimBackground = BuildFullScreenDim("DimBackground", new Color(0f, 0f, 0f, 0.97f));
        _dimTint       = BuildFullScreenDim("DimTint",       new Color(0.02f, 0.01f, 0.06f, 0.6f));

        BuildStatsPanel();
        BuildLeaderboardPanel();
        BuildArcadeEntryPanel();
    }

    private GameObject BuildFullScreenDim(string name, Color color)
    {
        GameObject dim = new GameObject(name);
        dim.transform.SetParent(transform, false);
        Image img = dim.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = dim.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return dim;
    }

    /// <summary>
    /// Creates a panel GameObject with a rounded-rect Image background.
    /// </summary>
    private GameObject CreateRoundedPanel(string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Image img = go.AddComponent<Image>();
        img.sprite = UIRoundedSprite.Default;
        img.type   = Image.Type.Sliced;
        img.color  = color;
        img.raycastTarget = false;
        return go;
    }

    // ── Stats panel (GAME OVER + score/level/lines) ─────────────────────────────

    private void BuildStatsPanel()
    {
        _statsPanel = CreateRoundedPanel("StatsPanel", new Color(0.06f, 0.06f, 0.09f, 0.92f));
        RectTransform rt = _statsPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(120f, 0f);
        rt.sizeDelta        = new Vector2(720f, 600f);

        CreateLabel(_statsPanel.transform, "GAME OVER",
            anchoredPos: new Vector2(0f, 220f), size: new Vector2(700f, 120f),
            fontSize: 96f, color: new Color(1f, 0.22f, 0.22f), style: FontStyles.Bold);

        CreateLabel(_statsPanel.transform, "────────────────────",
            anchoredPos: new Vector2(0f, 150f), size: new Vector2(700f, 40f),
            fontSize: 28f, color: new Color(0.5f, 0.5f, 0.5f), style: FontStyles.Normal);

        _scoreText = CreateLabel(_statsPanel.transform, "SCORE   0",
            anchoredPos: new Vector2(0f, 70f), size: new Vector2(700f, 80f),
            fontSize: 52f, color: Color.white, style: FontStyles.Bold);

        _levelText = CreateLabel(_statsPanel.transform, "LEVEL   1",
            anchoredPos: new Vector2(0f, 0f), size: new Vector2(700f, 60f),
            fontSize: 38f, color: new Color(0.5f, 0.85f, 1f), style: FontStyles.Normal);

        _linesText = CreateLabel(_statsPanel.transform, "LINES   0",
            anchoredPos: new Vector2(0f, -60f), size: new Vector2(700f, 60f),
            fontSize: 38f, color: new Color(0.5f, 0.85f, 1f), style: FontStyles.Normal);

        CreateLabel(_statsPanel.transform, "────────────────────",
            anchoredPos: new Vector2(0f, -130f), size: new Vector2(700f, 40f),
            fontSize: 28f, color: new Color(0.5f, 0.5f, 0.5f), style: FontStyles.Normal);

        _hintText = CreateLabel(_statsPanel.transform, "PRESS ANY KEY TO PLAY AGAIN",
            anchoredPos: new Vector2(0f, -200f), size: new Vector2(700f, 50f),
            fontSize: 28f, color: new Color(1f, 0.95f, 0.2f), style: FontStyles.Bold);
    }

    // ── Leaderboard panel ────────────────────────────────────────────────────────

    private void BuildLeaderboardPanel()
    {
        _leaderboardPanel = CreateRoundedPanel("LeaderboardPanel", new Color(0.06f, 0.06f, 0.09f, 0.94f));
        RectTransform rt = _leaderboardPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-120f, 0f);
        rt.sizeDelta        = new Vector2(720f, 780f);

        CreateLabel(_leaderboardPanel.transform, "LEADERBOARD",
            anchoredPos: new Vector2(0f, 340f), size: new Vector2(700f, 80f),
            fontSize: 48f, color: new Color(1f, 0.82f, 0.2f), style: FontStyles.Bold);

        CreateLabel(_leaderboardPanel.transform, "────────────────────",
            anchoredPos: new Vector2(0f, 290f), size: new Vector2(700f, 30f),
            fontSize: 24f, color: new Color(0.5f, 0.5f, 0.5f), style: FontStyles.Normal);

        CreateLabel(_leaderboardPanel.transform, "#",
            anchoredPos: new Vector2(-280f, 250f), size: new Vector2(80f, 40f),
            fontSize: 22f, color: new Color(0.7f, 0.7f, 0.7f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);
        CreateLabel(_leaderboardPanel.transform, "NAME",
            anchoredPos: new Vector2(-100f, 250f), size: new Vector2(220f, 40f),
            fontSize: 22f, color: new Color(0.7f, 0.7f, 0.7f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left);
        CreateLabel(_leaderboardPanel.transform, "SCORE",
            anchoredPos: new Vector2(220f, 250f), size: new Vector2(200f, 40f),
            fontSize: 22f, color: new Color(0.7f, 0.7f, 0.7f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Right);

        const float rowHeight = 44f;
        const float topY      = 210f;

        for (int i = 0; i < Leaderboard.MaxEntries; i++)
        {
            float y = topY - i * rowHeight;

            GameObject rowBg = new GameObject($"Row{i}Bg");
            rowBg.transform.SetParent(_leaderboardPanel.transform, false);
            Image rowImg = rowBg.AddComponent<Image>();
            rowImg.sprite = UIRoundedSprite.Default;
            rowImg.type   = Image.Type.Sliced;
            rowImg.color  = new Color(1f, 1f, 1f, 0f);
            rowImg.raycastTarget = false;
            RectTransform rbRt = rowBg.GetComponent<RectTransform>();
            rbRt.anchorMin = new Vector2(0.5f, 0.5f);
            rbRt.anchorMax = new Vector2(0.5f, 0.5f);
            rbRt.pivot     = new Vector2(0.5f, 0.5f);
            // Nudge the highlight right + widen it so it wraps the score
            // column (which extends to x ≈ +320 with its 200-wide right-aligned
            // text). 40 px right-bias, 700 px wide.
            rbRt.anchoredPosition = new Vector2(40f, y);
            rbRt.sizeDelta        = new Vector2(700f, rowHeight - 4f);
            _lbRowBgs.Add(rowImg);

            _lbRankTexts.Add(CreateLabel(_leaderboardPanel.transform, $"{i + 1}.",
                anchoredPos: new Vector2(-280f, y), size: new Vector2(80f, rowHeight),
                fontSize: 26f, color: new Color(0.85f, 0.85f, 0.85f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));

            _lbNameTexts.Add(CreateLabel(_leaderboardPanel.transform, "---",
                anchoredPos: new Vector2(-100f, y), size: new Vector2(220f, rowHeight),
                fontSize: 26f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Left));

            _lbScoreTexts.Add(CreateLabel(_leaderboardPanel.transform, "---",
                anchoredPos: new Vector2(220f, y), size: new Vector2(200f, rowHeight),
                fontSize: 26f, color: new Color(0.5f, 0.85f, 1f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Right));
        }
    }

    private void RenderLeaderboard(int highlightRank)
    {
        var entries = Leaderboard.Load();
        for (int i = 0; i < Leaderboard.MaxEntries; i++)
        {
            if (i < entries.Count)
            {
                _lbRankTexts[i].text  = $"{i + 1}.";
                _lbNameTexts[i].text  = entries[i].name;
                _lbScoreTexts[i].text = entries[i].score.ToString("N0");
            }
            else
            {
                _lbRankTexts[i].text  = $"{i + 1}.";
                _lbNameTexts[i].text  = "---";
                _lbScoreTexts[i].text = "---";
            }

            bool highlight = (highlightRank == i + 1);
            _lbRowBgs[i].color = highlight
                ? new Color(1f, 0.82f, 0.2f, 0.28f)
                : new Color(1f, 1f, 1f, 0f);

            Color nameCol = highlight ? new Color(1f, 0.95f, 0.2f) : Color.white;
            _lbNameTexts[i].color = (i < entries.Count) ? nameCol : new Color(0.35f, 0.35f, 0.35f);
        }
    }

    // ── Arcade entry panel (replaces leaderboard while entering initials) ────────

    private void BuildArcadeEntryPanel()
    {
        _arcadeEntryPanel = CreateRoundedPanel("ArcadeEntryPanel", new Color(0.06f, 0.06f, 0.09f, 0.94f));
        RectTransform rt = _arcadeEntryPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-120f, 0f);
        rt.sizeDelta        = new Vector2(720f, 780f);

        CreateLabel(_arcadeEntryPanel.transform, "NEW HIGH SCORE!",
            anchoredPos: new Vector2(0f, 320f), size: new Vector2(700f, 80f),
            fontSize: 48f, color: new Color(1f, 0.82f, 0.2f), style: FontStyles.Bold);

        CreateLabel(_arcadeEntryPanel.transform, "ENTER YOUR INITIALS",
            anchoredPos: new Vector2(0f, 260f), size: new Vector2(700f, 50f),
            fontSize: 28f, color: new Color(0.85f, 0.85f, 0.85f), style: FontStyles.Normal);

        // Three letter slots, evenly spaced.
        const float slotSize   = 140f;
        const float slotGap    = 40f;
        float totalWidth = NameLength * slotSize + (NameLength - 1) * slotGap;
        float startX = -totalWidth * 0.5f + slotSize * 0.5f;

        for (int i = 0; i < NameLength; i++)
        {
            float x = startX + i * (slotSize + slotGap);

            // Slot box (rounded)
            GameObject slot = new GameObject($"Slot{i}");
            slot.transform.SetParent(_arcadeEntryPanel.transform, false);
            Image slotImg = slot.AddComponent<Image>();
            slotImg.sprite = UIRoundedSprite.Default;
            slotImg.type   = Image.Type.Sliced;
            slotImg.color  = new Color(0.10f, 0.10f, 0.15f, 0.95f);
            slotImg.raycastTarget = false;
            RectTransform slotRt = slot.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot     = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = new Vector2(x, 80f);
            slotRt.sizeDelta        = new Vector2(slotSize, slotSize);
            _slotBoxes.Add(slotImg);

            // Letter inside
            _slotLetters.Add(CreateLabel(_arcadeEntryPanel.transform, "A",
                anchoredPos: new Vector2(x, 80f), size: new Vector2(slotSize, slotSize),
                fontSize: 96f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));

            // Up arrow hint
            CreateLabel(_arcadeEntryPanel.transform, "▲",
                anchoredPos: new Vector2(x, 180f), size: new Vector2(slotSize, 40f),
                fontSize: 32f, color: new Color(0.45f, 0.45f, 0.55f), style: FontStyles.Normal);
            // Down arrow hint
            CreateLabel(_arcadeEntryPanel.transform, "▼",
                anchoredPos: new Vector2(x, -20f), size: new Vector2(slotSize, 40f),
                fontSize: 32f, color: new Color(0.45f, 0.45f, 0.55f), style: FontStyles.Normal);
        }

        // Instructions footer
        CreateLabel(_arcadeEntryPanel.transform, "▲ ▼  CHANGE LETTER",
            anchoredPos: new Vector2(0f, -150f), size: new Vector2(700f, 40f),
            fontSize: 24f, color: new Color(0.75f, 0.75f, 0.8f), style: FontStyles.Normal);

        CreateLabel(_arcadeEntryPanel.transform, "◀ ▶  MOVE CURSOR",
            anchoredPos: new Vector2(0f, -200f), size: new Vector2(700f, 40f),
            fontSize: 24f, color: new Color(0.75f, 0.75f, 0.8f), style: FontStyles.Normal);

        CreateLabel(_arcadeEntryPanel.transform, "ENTER  CONFIRM",
            anchoredPos: new Vector2(0f, -250f), size: new Vector2(700f, 40f),
            fontSize: 24f, color: new Color(1f, 0.95f, 0.2f), style: FontStyles.Bold);
    }

    // ── Generic label helper ─────────────────────────────────────────────────────

    private TextMeshProUGUI CreateLabel(Transform parent, string text,
        Vector2 anchoredPos, Vector2 size,
        float fontSize, Color color, FontStyles style,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        string objName = text.Length > 16 ? text.Substring(0, 16) : text;
        if (string.IsNullOrEmpty(objName)) objName = "Label";

        GameObject obj = new GameObject(objName);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.color         = color;
        tmp.fontStyle     = style;
        tmp.alignment     = alignment;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;

        return tmp;
    }
}
