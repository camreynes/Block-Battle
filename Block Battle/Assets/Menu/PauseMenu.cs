using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BLOCK BATTLE - Pause Menu.
///
/// Self-bootstrapping pause overlay that appears when the player presses the
/// PAUSE input action (Esc on keyboard, Start on gamepad, Joystick trigger on
/// the arcade cabinet — see TetrixControls.inputactions).
///
/// Style mirrors MainMenu / GameOverScreen:
///   - Two stacked dim layers (DimBackground + DimTint) blot out the play
///     field while the overlay is up.
///   - A drifting layer of tetromino silhouettes sits ON TOP of the dim
///     stack so the screen has motion while paused — it would otherwise
///     freeze totally because Time.timeScale=0. The pieces use unscaled
///     time and only Update while the overlay is open, so they cost
///     nothing during normal play.
///   - A centered rounded panel with three menu options: RESUME / RESTART /
///     QUIT TO MENU. Selected entry pulses, scales up, and shows a left
///     accent bar.
///   - Time.timeScale is set to 0 while paused; everything in this script
///     reads Time.unscaledTime / unscaledDeltaTime so animations keep moving.
///   - Fully controller-navigable: UP/DOWN cycles, ENTER selects, PAUSE
///     toggles the menu (resumes if open).
///
/// Bootstrap:
///   A [RuntimeInitializeOnLoadMethod] hook spawns one instance after the
///   first scene loads. The component DontDestroyOnLoad's itself, so it
///   survives scene reloads (e.g. Restart) and the same instance handles
///   pause across the whole session. Menu scenes are harmless to ignore —
///   the overlay only opens when PAUSE fires, and the menu scene doesn't
///   register a player so PAUSE never fires there. We additionally guard
///   against pausing from menu/loading scenes by checking the scene name.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    /// <summary>
    /// True while the pause overlay is open. Gameplay scripts (e.g. PieceController)
    /// read this to suspend input handling — Time.timeScale=0 freezes coroutines
    /// that use Time.deltaTime, but MonoBehaviour.Update() keeps firing every frame
    /// regardless of timeScale, so input-driven systems have to gate themselves.
    /// </summary>
    public bool IsPaused => _isPaused;

    // ── Bootstrapping ─────────────────────────────────────────────────────────

    // Spawn one instance once the game starts. We don't need a per-scene
    // instance because DontDestroyOnLoad keeps us alive across reloads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("PauseMenu");
        go.AddComponent<PauseMenu>();
    }

    // ── Inspector / config ────────────────────────────────────────────────────

    // Scene name of the main menu — used by "QUIT TO MENU". Has to match
    // what's in Build Settings; if you rename the scene, update this too.
    private const string MainMenuSceneName = "MainMenu";

    // Scenes where pausing is allowed. Anything else (the menu, loading
    // screens) is ignored. Keeps Esc / Start from popping the overlay over
    // unrelated screens.
    private static readonly System.Collections.Generic.HashSet<string> PausableScenes =
        new() { "Singleplayer", "Multiplayer" };

    // Menu options, drawn top → bottom in this order.
    private enum MenuOption { Resume = 0, Restart = 1, Quit = 2 }
    private static readonly string[] OptionLabels = { "RESUME", "RESTART", "QUIT TO MENU" };

    // ── UI references (built at runtime) ──────────────────────────────────────

    private GameObject _dimBackground;
    private GameObject _dimTint;
    private GameObject _bgPiecesLayer;       // parents the drifting tetromino silhouettes
    private GameObject _panel;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _hintText;

    // One row per option: container, bg, accent bar, label.
    private readonly System.Collections.Generic.List<RectTransform>    _optionRects   = new();
    private readonly System.Collections.Generic.List<Image>            _optionBgs     = new();
    private readonly System.Collections.Generic.List<Image>            _optionAccents = new();
    private readonly System.Collections.Generic.List<TextMeshProUGUI>  _optionLabels  = new();

    // Drifting background tetrominoes — same idea as MainMenu.cs. We keep the
    // list around between Open/Resume and only animate while paused, so the
    // pieces cost essentially nothing during normal gameplay.
    private readonly System.Collections.Generic.List<FallingPiece>     _bgPieces      = new();

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isPaused;
    private MenuOption _selected = MenuOption.Resume;

    // Grace window after Show() so the keystroke that opened the menu doesn't
    // immediately count as a confirm/cancel on the same frame.
    private float _inputReadyAt;
    private const float InputGraceSeconds = 0.12f;

    // DAS for held UP/DOWN navigation.
    private float _navRepeatTimer = 0f;
    private const float NavInitialDelay = 0.35f;
    private const float NavRepeatRate   = 0.10f;

    // We only listen to TetrixInputManager when a player has been registered
    // for that action; before InitializeGrids runs, those calls return false.
    // Bug-prone otherwise — see TetrixInputManager.WasPressed.
    private const int QueryPlayerId = 0;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureEventSystem();
        BuildCanvas();
        HideAll();
    }

    private void Update()
    {
        // Toggle PAUSE: open if not paused (and we're in a pausable scene),
        // resume if paused.
        if (PausePressed())
        {
            if (_isPaused) Resume();
            else if (PausableScenes.Contains(SceneManager.GetActiveScene().name)) Open();
            return; // Don't double-process navigation on the same frame.
        }

        if (!_isPaused) return;

        // The background pieces drift on unscaled time so they keep moving
        // while Time.timeScale == 0. They animate independently of input
        // grace so the screen never looks frozen even on the first frame.
        AnimateBackground();

        // Grace window — swallow input briefly after opening so the same key
        // press doesn't both open the menu AND select the first option.
        if (Time.unscaledTime < _inputReadyAt) return;

        HandleNavigation();
        AnimateOptions();
    }

    // ── Pause toggle plumbing ────────────────────────────────────────────────

    /// <summary>True on the frame PAUSE was pressed by any registered source.</summary>
    private static bool PausePressed()
    {
        // Prefer the TetrixInputManager binding because it covers Esc + gamepad
        // Start + arcade-stick trigger in one shot. If no player is registered
        // yet (e.g. the menu scene), it returns false silently — fine.
        if (TetrixInputManager.WasPressed(GameInputAction.PAUSE, QueryPlayerId)) return true;

        // Direct fallbacks so the menu still opens before/after a player is
        // registered (loading transitions, between rounds in future MP modes).
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.startButton.wasPressedThisFrame) return true;

        return false;
    }

    private void Open()
    {
        _isPaused = true;
        _selected = MenuOption.Resume;
        _inputReadyAt = Time.unscaledTime + InputGraceSeconds;
        _navRepeatTimer = NavInitialDelay;

        Time.timeScale = 0f;

        if (_dimBackground  != null) _dimBackground.SetActive(true);
        if (_dimTint        != null) _dimTint.SetActive(true);
        if (_bgPiecesLayer  != null) _bgPiecesLayer.SetActive(true);
        if (_panel          != null) _panel.SetActive(true);

        ApplySelection();
    }

    private void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        HideAll();
    }

    private void Restart()
    {
        // Same restart flow GameOverScreen uses — reload the active scene and
        // unfreeze time before the new scene starts.
        _isPaused = false;
        Time.timeScale = 1f;
        HideAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitToMenu()
    {
        // Guard against a class of "menu freezes after returning" bugs that
        // come from leftover state on the persistent EventSystem and on this
        // PauseMenu instance itself. Concretely:
        //
        //   * EventSystem.currentSelectedGameObject can still point at the
        //     destroyed pause-button row from the unloaded scene; the next
        //     scene's first nav input then routes to a dead reference and
        //     looks like the menu is unresponsive. SetSelectedGameObject(null)
        //     forces the new scene's UI to start with no selection.
        //   * Time.timeScale could in theory be left at 0 if anything threw
        //     between toggling _isPaused and getting here. Reset defensively.
        //   * Any coroutines we might have started are torn down before the
        //     scene swap so they can't tick into the new scene's frame and
        //     fight whatever spawns there.
        //
        // None of these are confirmed root causes — they're cheap belt-and-
        // braces fixes for a freeze that's hard to repro in the editor.
        _isPaused = false;
        Time.timeScale = 1f;
        HideAll();
        StopAllCoroutines();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void HideAll()
    {
        if (_dimBackground  != null) _dimBackground.SetActive(false);
        if (_dimTint        != null) _dimTint.SetActive(false);
        if (_bgPiecesLayer  != null) _bgPiecesLayer.SetActive(false);
        if (_panel          != null) _panel.SetActive(false);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void HandleNavigation()
    {
        // Movement: edge-triggered first, then DAS while held.
        int dy = 0;
        bool edge = false;

        if (NavUpPressed())   { dy = -1; edge = true; }
        if (NavDownPressed()) { dy = +1; edge = true; }

        if (!edge)
        {
            int hy = 0;
            if (NavUpHeld())   hy = -1;
            if (NavDownHeld()) hy = +1;

            if (hy != 0)
            {
                _navRepeatTimer -= Time.unscaledDeltaTime;
                if (_navRepeatTimer <= 0f)
                {
                    dy = hy;
                    _navRepeatTimer = NavRepeatRate;
                }
            }
            else
            {
                _navRepeatTimer = NavInitialDelay;
            }
        }
        else
        {
            _navRepeatTimer = NavInitialDelay;
        }

        if (dy != 0)
        {
            int count = OptionLabels.Length;
            _selected = (MenuOption)(((int)_selected + dy + count) % count);
            ApplySelection();
        }

        // Confirm
        if (ConfirmPressed())
        {
            switch (_selected)
            {
                case MenuOption.Resume:  Resume();     break;
                case MenuOption.Restart: Restart();    break;
                case MenuOption.Quit:    QuitToMenu(); break;
            }
        }
    }

    // Each input direction queried via:
    //   1. TetrixInputManager (joystick + bound keyboard) — if a player is registered
    //   2. Direct Keyboard.current arrows / WASD
    //   3. Direct Gamepad.current dpad / left stick
    // This mirrors MainMenu's HandleInput, which already does (2) and (3) by
    // hand because the menu scene doesn't register a player. Doing both keeps
    // pause working in any scene.

    private static bool NavUpPressed()
    {
        if (TetrixInputManager.WasPressed(GameInputAction.UP, QueryPlayerId)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame)) return true;
        return false;
    }

    private static bool NavDownPressed()
    {
        if (TetrixInputManager.WasPressed(GameInputAction.DOWN, QueryPlayerId)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame)) return true;
        return false;
    }

    private static bool NavUpHeld()
    {
        if (TetrixInputManager.IsHeld(GameInputAction.UP, QueryPlayerId)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed)) return true;
        return false;
    }

    private static bool NavDownHeld()
    {
        if (TetrixInputManager.IsHeld(GameInputAction.DOWN, QueryPlayerId)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed)) return true;
        return false;
    }

    private static bool ConfirmPressed()
    {
        if (TetrixInputManager.WasPressed(GameInputAction.ENTER, QueryPlayerId)) return true;
        // Hard drop is the arcade "go button" — also a natural confirm.
        if (TetrixInputManager.WasPressed(GameInputAction.HARD_DROP, QueryPlayerId)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame ||
                           kb.numpadEnterKey.wasPressedThisFrame ||
                           kb.spaceKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    // ── Visual state ─────────────────────────────────────────────────────────

    private void ApplySelection()
    {
        // Re-render every option's appearance based on whether it's the
        // currently selected one. AnimateOptions handles the per-frame pulsing.
        for (int i = 0; i < OptionLabels.Length; i++)
        {
            bool isCurrent = (i == (int)_selected);

            _optionLabels[i].color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.65f);
            _optionAccents[i].color = isCurrent
                ? new Color(0f, 0.85f, 1f, 1f)
                : new Color(0f, 0.85f, 1f, 0f);
        }
    }

    private void AnimateOptions()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _optionRects.Count; i++)
        {
            bool isCurrent = (i == (int)_selected);

            // Selected pulses subtly; unselected stay flat.
            float targetScale = isCurrent ? 1.06f + 0.02f * Mathf.Sin(t * 5f) : 1.0f;
            Vector3 s = _optionRects[i].localScale;
            s.x = Mathf.Lerp(s.x, targetScale, 14f * Time.unscaledDeltaTime);
            s.y = Mathf.Lerp(s.y, targetScale, 14f * Time.unscaledDeltaTime);
            _optionRects[i].localScale = new Vector3(s.x, s.y, 1f);

            Color bgTarget = isCurrent
                ? new Color(0.12f, 0.18f, 0.32f, 0.95f)
                : new Color(0.07f, 0.08f, 0.12f, 0.85f);
            _optionBgs[i].color = Color.Lerp(_optionBgs[i].color, bgTarget, 10f * Time.unscaledDeltaTime);
        }
    }

    // ── Canvas / UI construction ─────────────────────────────────────────────

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
        // Sit above gameplay (sortingOrder ~10) but below GameOverScreen (200)
        // so dying mid-pause still shows the game-over panel on top.
        canvas.sortingOrder = 150;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Match GameOverScreen's two-stack dim treatment so the look stays
        // consistent across overlays. Slightly less opaque so the game grid
        // is faintly visible behind the menu — players like seeing what they
        // paused on.
        _dimBackground = BuildFullScreenDim("DimBackground", new Color(0f, 0f, 0f, 0.78f));
        _dimTint       = BuildFullScreenDim("DimTint",       new Color(0.02f, 0.01f, 0.06f, 0.45f));

        // Falling-block layer sits between the dim stack and the menu panel
        // so silhouettes drift in front of the dimmed playfield but behind
        // the option card. Sibling order (= draw order in ScreenSpaceOverlay)
        // is implicit from the order children are added to `transform`.
        BuildFallingPieces(count: 9);

        BuildPanel();
    }

    private GameObject BuildFullScreenDim(string name, Color color)
    {
        GameObject dim = new GameObject(name);
        dim.transform.SetParent(transform, false);
        Image img = dim.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        RectTransform rt = dim.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return dim;
    }

    private void BuildPanel()
    {
        // Centered card. Sized so all three options + the title fit comfortably.
        _panel = new GameObject("PausePanel");
        _panel.transform.SetParent(transform, false);
        Image bg = _panel.AddComponent<Image>();
        bg.sprite        = UIRoundedSprite.Default;
        bg.type          = Image.Type.Sliced;
        bg.color         = new Color(0.05f, 0.06f, 0.10f, 0.94f);
        bg.raycastTarget = false;

        RectTransform rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(720f, 600f);

        // Cyan accent stripe across the top — same flavor as MainMenu panels.
        GameObject stripe = new GameObject("AccentStripe");
        stripe.transform.SetParent(_panel.transform, false);
        Image sImg = stripe.AddComponent<Image>();
        sImg.sprite = UIRoundedSprite.Default;
        sImg.type   = Image.Type.Sliced;
        sImg.color  = new Color(0f, 0.85f, 1f, 1f);
        sImg.raycastTarget = false;
        RectTransform sRt = stripe.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot     = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -10f);
        sRt.sizeDelta        = new Vector2(-40f, 8f);

        _titleText = CreateLabel(_panel.transform, "PAUSED",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -50f), size: new Vector2(700f, 100f),
            fontSize: 72f, color: new Color(0f, 0.9f, 1f), style: FontStyles.Bold);

        // Three big stacked option buttons. Centered and equally spaced.
        const float optWidth  = 560f;
        const float optHeight = 88f;
        const float optGap    = 16f;
        const float firstY    = -200f; // y of the first row's center

        for (int i = 0; i < OptionLabels.Length; i++)
        {
            float y = firstY - i * (optHeight + optGap);

            GameObject row = new GameObject($"Option_{OptionLabels[i]}");
            row.transform.SetParent(_panel.transform, false);
            RectTransform rrt = row.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 1f);
            rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.pivot     = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, y);
            rrt.sizeDelta        = new Vector2(optWidth, optHeight);
            _optionRects.Add(rrt);

            // BG (rounded card, click-target).
            GameObject bgGo = new GameObject("bg");
            bgGo.transform.SetParent(row.transform, false);
            Image bImg = bgGo.AddComponent<Image>();
            bImg.sprite        = UIRoundedSprite.Default;
            bImg.type          = Image.Type.Sliced;
            bImg.color         = new Color(0.07f, 0.08f, 0.12f, 0.85f);
            bImg.raycastTarget = true;
            RectTransform brt = bgGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            _optionBgs.Add(bImg);

            // Wire mouse clicks too — players moving over the menu shouldn't
            // need to drop the mouse. Selecting via click also moves the
            // navigation cursor so the next ENTER press matches expectations.
            int captureIdx = i;
            Button btn = row.AddComponent<Button>();
            btn.targetGraphic = bImg;
            btn.onClick.AddListener(() =>
            {
                _selected = (MenuOption)captureIdx;
                ApplySelection();
                if (Time.unscaledTime >= _inputReadyAt)
                {
                    switch (_selected)
                    {
                        case MenuOption.Resume:  Resume();     break;
                        case MenuOption.Restart: Restart();    break;
                        case MenuOption.Quit:    QuitToMenu(); break;
                    }
                }
            });

            // Left accent bar — only visible on the selected option.
            GameObject accent = new GameObject("accent");
            accent.transform.SetParent(row.transform, false);
            Image aImg = accent.AddComponent<Image>();
            aImg.sprite        = UIRoundedSprite.Default;
            aImg.type          = Image.Type.Sliced;
            aImg.color         = new Color(0f, 0.85f, 1f, 0f);
            aImg.raycastTarget = false;
            RectTransform art = accent.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot     = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(10f, 0f);
            art.sizeDelta        = new Vector2(8f, -16f);
            _optionAccents.Add(aImg);

            _optionLabels.Add(CreateLabel(row.transform, OptionLabels[i],
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: Vector2.zero, size: new Vector2(optWidth - 40f, optHeight - 12f),
                fontSize: 38f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));
        }

        // Footer hint.
        _hintText = CreateLabel(_panel.transform, "▲ ▼  NAVIGATE     ENTER  SELECT     ESC / START  RESUME",
            anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 24f), size: new Vector2(700f, 36f),
            fontSize: 20f, color: new Color(1f, 1f, 1f, 0.65f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);
    }

    // ── Falling background pieces ────────────────────────────────────────────

    // Same trick as MainMenu.cs: spawn a fixed pool of tetromino silhouettes,
    // drift them down on Time.unscaledDeltaTime, wrap to the top with a fresh
    // x / color when they leave the bottom of the screen. Two reasons we
    // care about this on the pause overlay specifically: (1) Time.timeScale
    // is 0 while paused, so anything reading Time.deltaTime would freeze;
    // (2) the rest of the play field is dimmed and motionless, so without
    // some background motion the screen looks crashed rather than paused.
    private class FallingPiece
    {
        public RectTransform root;
        public float         speed;       // px/sec, drifts down
        public float         rotateSpeed; // deg/sec, around z
        public Color         color;
    }

    // Canonical tetromino cell offsets (centered on origin-ish). Cribbed from
    // MainMenu so the two screens visually match. Kept private+static here
    // rather than shared in a util class — the data is tiny and duplicating
    // it avoids coupling the menu and pause subsystems.
    private static readonly Vector2Int[][] Tetrominoes =
    {
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },   // I
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },    // O
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) },   // T
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 1) },  // J
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) },   // L
        new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },   // S
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1) },   // Z
    };

    private static Color RandomPieceColor()
    {
        // Classic tetromino palette, same as MainMenu.
        Color[] palette =
        {
            new(0.00f, 0.74f, 0.83f),  // I
            new(1.00f, 0.85f, 0.13f),  // O
            new(0.67f, 0.28f, 0.85f),  // T
            new(0.13f, 0.41f, 0.95f),  // J
            new(1.00f, 0.60f, 0.12f),  // L
            new(0.30f, 0.85f, 0.35f),  // S
            new(0.95f, 0.20f, 0.30f),  // Z
        };
        return palette[Random.Range(0, palette.Length)];
    }

    private void BuildFallingPieces(int count)
    {
        // Full-screen container for all the drifting pieces. Use the
        // RectTransform-from-the-start ctor so we don't accidentally end up
        // with both a Transform and a RectTransform on the same GameObject.
        _bgPiecesLayer = new GameObject("FallingPieces", typeof(RectTransform));
        _bgPiecesLayer.transform.SetParent(transform, false);
        RectTransform layerRt = (RectTransform)_bgPiecesLayer.transform;
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = Vector2.zero;
        layerRt.offsetMax = Vector2.zero;

        // Slightly fewer pieces than the main menu (9 vs 11) and a hair
        // slower — pause is meant to feel calmer than the menu, not busier.
        Rect screen = ((RectTransform)transform).rect;
        for (int i = 0; i < count; i++)
        {
            var piece = BuildOneFallingPiece(_bgPiecesLayer.transform);
            Vector2 pos = new Vector2(
                Random.Range(-screen.width  * 0.48f, screen.width  * 0.48f),
                Random.Range(-screen.height * 0.50f, screen.height * 0.50f));
            piece.root.anchoredPosition = pos;
            piece.speed       = Random.Range(28f, 78f);
            piece.rotateSpeed = Random.Range(-22f, 22f);
            _bgPieces.Add(piece);
        }
    }

    private FallingPiece BuildOneFallingPiece(Transform parent)
    {
        Vector2Int[] cells = Tetrominoes[Random.Range(0, Tetrominoes.Length)];

        GameObject root = new GameObject("Piece", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot     = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(220f, 220f); // doesn't matter; cells use absolute offsets

        Color c = RandomPieceColor();
        const float cellSize = 48f;
        // Lower alpha than MainMenu (0.18 vs 0.22) so the pieces don't fight
        // for attention with the option panel — they're atmosphere, not UI.
        const float cellAlpha = 0.18f;
        foreach (var cell in cells)
        {
            GameObject cellGo = new GameObject("cell");
            cellGo.transform.SetParent(root.transform, false);
            Image img = cellGo.AddComponent<Image>();
            img.sprite        = UIRoundedSprite.Default;
            img.type          = Image.Type.Sliced;
            img.color         = new Color(c.r, c.g, c.b, cellAlpha);
            img.raycastTarget = false;
            RectTransform rt = cellGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize - 4f, cellSize - 4f);
            rt.anchoredPosition = new Vector2(cell.x * cellSize, cell.y * cellSize);
        }

        return new FallingPiece { root = rr, color = c };
    }

    private void AnimateBackground()
    {
        // Drift each piece downward, wrap to the top with a new x / color
        // once it leaves the bottom edge. Speed and rotateSpeed were seeded
        // once at spawn for parallax-ish variation. All math is in unscaled
        // time so this keeps moving while the rest of the game is frozen.
        Rect screen   = ((RectTransform)transform).rect;
        float bottomY = -screen.height * 0.5f - 80f;
        float topY    =  screen.height * 0.5f + 80f;

        for (int i = 0; i < _bgPieces.Count; i++)
        {
            var p = _bgPieces[i];
            Vector2 pos = p.root.anchoredPosition;
            pos.y -= p.speed * Time.unscaledDeltaTime;

            if (pos.y < bottomY)
            {
                pos.y = topY;
                pos.x = Random.Range(-screen.width * 0.48f, screen.width * 0.48f);
                p.color = RandomPieceColor();
                foreach (Image img in p.root.GetComponentsInChildren<Image>())
                    img.color = new Color(p.color.r, p.color.g, p.color.b, 0.18f);
            }
            p.root.anchoredPosition = pos;
            p.root.localEulerAngles  = new Vector3(0f, 0f, Time.unscaledTime * p.rotateSpeed);
        }
    }

    // ── Label helper ─────────────────────────────────────────────────────────

    private TextMeshProUGUI CreateLabel(Transform parent, string text,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        float fontSize, Color color, FontStyles style,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        string objName = text.Length > 16 ? text.Substring(0, 16) : text;
        if (string.IsNullOrEmpty(objName)) objName = "Label";

        GameObject obj = new GameObject(objName);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot     = pivot;
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
