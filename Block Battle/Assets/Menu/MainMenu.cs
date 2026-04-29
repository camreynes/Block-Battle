using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BLOCK BATTLE - Main Menu / Intro Screen.
///
/// Self-bootstrapping Tetris-99-style menu with 5 tabs:
///   1. SINGLEPLAYER  (default - press ENTER to launch the Singleplayer scene)
///   2. MULTIPLAYER   (locked / grayed out - coming soon)
///   3. CONTROLS      (keyboard + joystick reference sheet)
///   4. LEADERBOARD   (reuses Leaderboard.cs - top 10)
///   5. ABOUT         (credits + game blurb)
///
/// Setup in Unity:
///   • Create an empty scene (e.g. "MainMenu.unity") and add it to Build Settings
///     BEFORE the "Singleplayer" scene.
///   • Add an empty GameObject, attach this MainMenu component.
///   • Optionally drop a Sprite into the "Background Image" field to replace
///     the procedural placeholder background.
///
/// Design patterns cribbed from GameOverScreen.cs: the entire canvas is built
/// at runtime (no prefabs), every panel uses UIRoundedSprite.Default for its
/// rounded-rect background, and input is polled through Keyboard/Gamepad
/// fallbacks (TetrixInputManager isn't registered on the menu scene because
/// no player exists yet).
///
/// Layout:
///   The five tabs live as a big vertical column on the LEFT THIRD of the
///   screen (prominent, readable cards). The selected tab's content fills the
///   RIGHT TWO-THIRDS. Hovering a tab adds an outline + scales it up.
///
/// Navigation:
///   UP / DOWN   (or W / S / ← / →)  → cycle tab
///   ENTER / SPACE                   → activate current tab
///   ESC                             → quit application
///   Click / Hover                   → select tab / show outline
/// 
///     This was made with claude, among a small percent of other files, due to a time crunch
///     after some changes, it is astounding how good claude is
/// </summary>
public class MainMenu : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Optional - drop a Sprite here to replace the placeholder BG.")]
    [SerializeField] private Sprite backgroundImage;

    [Header("Scene to load when Singleplayer is activated.")]
    [SerializeField] private string singleplayerSceneName = "Singleplayer";

    // ── Tab definitions ──────────────────────────────────────────────────────

    private enum TabID { Singleplayer = 0, Multiplayer = 1, Controls = 2, Leaderboard = 3, Stats = 4, About = 5 }

    // One color per tab — tetromino palette. Stats reuses the green S-piece tone.
    private static readonly Color[] TabColors =
    {
        new(0.00f, 0.74f, 0.83f, 1f),  // Singleplayer - I cyan
        new(0.40f, 0.40f, 0.45f, 1f),  // Multiplayer  - locked gray
        new(1.00f, 0.60f, 0.12f, 1f),  // Controls     - L orange
        new(1.00f, 0.85f, 0.13f, 1f),  // Leaderboard  - O yellow
        new(0.30f, 0.85f, 0.35f, 1f),  // Stats        - S green
        new(0.67f, 0.28f, 0.85f, 1f),  // About        - T purple
    };

    private static readonly string[] TabLabels = { "SINGLEPLAYER", "MULTIPLAYER", "CONTROLS", "LEADERBOARD", "STATS", "ABOUT" };

    // ── Layout constants (centralized so widths/anchors stay in sync) ────────

    // Left-column tabs - anchored top-left, pivot left-center so `basePos`
    // is the tab's left-middle point (easy to slide right on hover/select).
    // Heights/gaps were tightened when the Stats tab brought the count from
    // 5 to 6 — the old 108/22 spacing pushed the last tab off-screen at 1080p.
    private const float TabColX         = 60f;
    private const float TabColTopY      = -220f;   // y of the first tab's top edge
    private const float TabWidth        = 460f;
    private const float TabHeight       = 90f;
    private const float TabGap          = 16f;
    private const float TabOutlineInset = 6f;      // how far the outline peeks around the tab

    // Right-side content panel - anchored top-right, fills the remaining 2/3.
    private const float PanelMarginR = 60f;
    private const float PanelTopY    = -250f;
    private const float PanelWidth   = 1260f;
    private const float PanelHeight  = 720f;

    // ── Runtime state ────────────────────────────────────────────────────────

    private TabID _currentTab = TabID.Singleplayer;
    private readonly List<GameObject>       _tabButtons   = new();
    private readonly List<Image>            _tabBgs       = new();
    private readonly List<Image>            _tabOutlines  = new();
    private readonly List<TextMeshProUGUI>  _tabLabels    = new();
    private readonly List<RectTransform>    _tabRects     = new();
    private readonly List<TabHoverTracker>  _tabHovers    = new();
    private readonly List<GameObject>       _panels       = new();

    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _hintText;
    private TextMeshProUGUI _playPromptText;   // big flashing "PRESS ENTER" on SP tab
    private RectTransform   _rotatingTPiece;   // purple T-piece preview on SP tab
    private readonly List<FallingPiece> _bgPieces = new();

    // When input is ready (consumes leftover input from previous scene/frame)
    private float _inputReadyAt = 0f;
    private const float InputGraceSeconds = 0.1f;

    // ── Falling-block background data ────────────────────────────────────────

    // Each "falling piece" is one procedural tetromino made of four cells.
    // We just translate the parent RectTransform every frame and wrap to top
    // when it drifts past the bottom of the screen. Cheap and infinite.
    private class FallingPiece
    {
        public RectTransform root;
        public float         speed;       // px / second (scaled)
        public float         rotateSpeed; // deg / second
        public Color         color;
    }

    // Pointer hover tracking for a tab. Attached to the tab's container GO;
    // events bubble up from the child bg Image (raycastTarget=true) via Unity's
    // EventSystem, so hovering the card flips isHovered for AnimateTabs to read.
    private class TabHoverTracker : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        public bool isHovered;
        public void OnPointerEnter(PointerEventData _) { isHovered = true; }
        public void OnPointerExit(PointerEventData _)  { isHovered = false; }
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureEventSystem();
        BuildCanvas();
        SelectTab(TabID.Singleplayer, animate: false);
        _inputReadyAt = Time.unscaledTime + InputGraceSeconds;
    }

    private void Start()
    {
        // Music kick-off is in Start, not Awake, on purpose. Unity guarantees
        // that every component's Awake finishes before any Start runs, but it
        // does NOT guarantee the order of sibling Awakes — so if this lived in
        // Awake it'd race against IntroSFXManager.Awake, which is the thing
        // that sets IntroSFXManager.Instance. ~50% of the time we'd hit the
        // null-conditional `?.` while Instance was still null, the call would
        // silently no-op, and the menu would launch in dead silence even
        // though the manager is in the scene with clips assigned.
        //
        // The give-away that this was an Awake-order race rather than a
        // missing-clip problem: PlayTabChange (called later, from input)
        // worked fine while PlayIntroMusic (called from Awake) didn't.
        //
        // Null-safe: if no IntroSFXManager exists in the scene at all the
        // menu still runs silently rather than throwing. The music itself
        // also no-ops cleanly if both intro track slots are empty.
        IntroSFXManager.Instance?.PlayIntroMusic();
    }

    private void Update()
    {
        AnimateBackground();
        AnimateTitle();
        AnimateTabs();
        AnimateSingleplayerTab();

        if (Time.unscaledTime < _inputReadyAt) return;
        HandleInput();
    }

    // ── Input ────────────────────────────────────────────────────────────────

    private void HandleInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // Tabs are now a vertical column, so UP/DOWN is the natural axis.
        // LEFT/RIGHT is kept as an alias for muscle memory from the old layout.
        bool prev = (kb != null && (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame
                                 || kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame))
                 || (gp != null && (gp.dpad.up.wasPressedThisFrame       || gp.leftStick.up.wasPressedThisFrame
                                 || gp.dpad.left.wasPressedThisFrame     || gp.leftStick.left.wasPressedThisFrame));
        bool next = (kb != null && (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame
                                 || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                 || (gp != null && (gp.dpad.down.wasPressedThisFrame     || gp.leftStick.down.wasPressedThisFrame
                                 || gp.dpad.right.wasPressedThisFrame    || gp.leftStick.right.wasPressedThisFrame));
        bool enter = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                  || (gp != null &&  gp.buttonSouth.wasPressedThisFrame);
        bool quit  = (kb != null &&  kb.escapeKey.wasPressedThisFrame);

        if (prev)  SelectTab((TabID)(((int)_currentTab + TabColors.Length - 1) % TabColors.Length));
        if (next)  SelectTab((TabID)(((int)_currentTab + 1) % TabColors.Length));
        if (enter) ActivateCurrentTab();
        if (quit)  QuitGame();
    }

    private void ActivateCurrentTab()
    {
        switch (_currentTab)
        {
            case TabID.Singleplayer:
                SceneManager.LoadScene(singleplayerSceneName);
                break;
            case TabID.Multiplayer:
                // Shake the locked panel as feedback.
                StopAllCoroutines();
                StartCoroutine(ShakeLockedPanel());
                break;
            // Other tabs are info-only - nothing to activate.
        }
    }

    private System.Collections.IEnumerator ShakeLockedPanel()
    {
        RectTransform rt = _panels[(int)TabID.Multiplayer].GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(t * 80f) * 14f * (1f - t / dur);
            rt.anchoredPosition = basePos + new Vector2(k, 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Tab switching ────────────────────────────────────────────────────────

    private void SelectTab(TabID tab, bool animate = true)
    {
        // Only fire the tick when the selection actually moves AND we're past
        // the initial silent setup. `animate=false` is used by Awake's initial
        // SelectTab call, which shouldn't play a sound before the menu has
        // even appeared. `tab != _currentTab` guards against a rapid repeat
        // click on the already-active tab spamming the SFX.
        bool changed = (tab != _currentTab);
        _currentTab = tab;
        for (int i = 0; i < _panels.Count; i++)
            _panels[i].SetActive(i == (int)tab);

        if (animate && changed)
            IntroSFXManager.Instance?.PlayTabChange();

        // Refresh dynamic content on entry.
        if (tab == TabID.Leaderboard) RefreshLeaderboardPanel();
        if (tab == TabID.Stats)       RefreshStatsPanel();

        // Footer hint per tab.
        switch (tab)
        {
            case TabID.Singleplayer: _hintText.text = "▲ ▼  CHANGE TAB     ENTER / SPACE  START GAME     ESC  QUIT"; break;
            case TabID.Multiplayer:  _hintText.text = "▲ ▼  CHANGE TAB     MULTIPLAYER COMING SOON";                 break;
            case TabID.Controls:     _hintText.text = "▲ ▼  CHANGE TAB     ARCADE CABINET BINDINGS";                break;
            case TabID.Leaderboard:  _hintText.text = "▲ ▼  CHANGE TAB     LOCAL TOP-10 SCORES";                     break;
            case TabID.Stats:        _hintText.text = "▲ ▼  CHANGE TAB     LIFETIME STATS";                         break;
            case TabID.About:        _hintText.text = "▲ ▼  CHANGE TAB     ABOUT BLOCK BATTLE";                      break;
        }
    }

    // ── Animations ───────────────────────────────────────────────────────────

    private float _titleHue = 0f;
    private void AnimateTitle()
    {
        // Slowly cycle the title hue - gives the top banner a subtle rainbow
        // pulse that never sits still. Using HSV so saturation stays vivid.
        _titleHue = Mathf.Repeat(_titleHue + Time.unscaledDeltaTime * 0.08f, 1f);
        _titleText.color = Color.HSVToRGB(_titleHue, 0.85f, 1f);
    }

    private void AnimateTabs()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _tabRects.Count; i++)
        {
            bool isCurrent = (i == (int)_currentTab);
            bool isHovered = _tabHovers[i] != null && _tabHovers[i].isHovered;

            // Scale: selected > hovered > idle. Smoothly lerp so it's buttery.
            float targetScale = isCurrent ? 1.10f : (isHovered ? 1.06f : 1.00f);
            Vector3 s = _tabRects[i].localScale;
            s.x = Mathf.Lerp(s.x, targetScale, 12f * Time.unscaledDeltaTime);
            s.y = Mathf.Lerp(s.y, targetScale, 12f * Time.unscaledDeltaTime);
            _tabRects[i].localScale = new Vector3(s.x, s.y, 1f);

            // Tabs are anchored - selection is communicated via scale, bg color
            // and the pulsing outline only. Cam specifically wants the column to
            // stay rock-solid when flipping tabs, so no horizontal slide on
            // either the selected OR the hovered state. Lerp toward the stored
            // baseline each frame in case an earlier animation (or the shake
            // coroutine on the locked panel) nudged things off-axis.
            Vector2 baseAnchor = (Vector2)_tabRects[i].GetComponent<TabBaseline>().basePos;
            _tabRects[i].anchoredPosition = Vector2.Lerp(
                _tabRects[i].anchoredPosition, baseAnchor, 14f * Time.unscaledDeltaTime);

            // Tab background. Multiplayer (locked) never brightens; normal tabs
            // get a mid tone on hover so the active state still stands out.
            Color baseCol = TabColors[i];
            Color bgTarget = i == (int)TabID.Multiplayer
                ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.55f)
                : isCurrent
                    ? baseCol
                    : isHovered
                        ? new Color(baseCol.r * 0.85f, baseCol.g * 0.85f, baseCol.b * 0.85f, 0.95f)
                        : new Color(baseCol.r * 0.55f, baseCol.g * 0.55f, baseCol.b * 0.55f, 0.85f);
            _tabBgs[i].color = Color.Lerp(_tabBgs[i].color, bgTarget, 8f * Time.unscaledDeltaTime);

            Color labelTarget = (isCurrent || isHovered)
                ? Color.white
                : new Color(1f, 1f, 1f, i == (int)TabID.Multiplayer ? 0.55f : 0.78f);
            _tabLabels[i].color = Color.Lerp(_tabLabels[i].color, labelTarget, 8f * Time.unscaledDeltaTime);

            // Outline: pulsing white when selected, tinted glow when hovered,
            // invisible otherwise. The outline image sits BEHIND the bg (earlier
            // sibling in the tab container), so only its rim peeks out.
            Color outlineTarget;
            if (isCurrent)
                outlineTarget = new Color(1f, 1f, 1f, 0.85f + 0.15f * Mathf.Sin(t * 5f));
            else if (isHovered)
                outlineTarget = new Color(baseCol.r, baseCol.g, baseCol.b, 0.85f);
            else
                outlineTarget = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
            _tabOutlines[i].color = Color.Lerp(
                _tabOutlines[i].color, outlineTarget, 12f * Time.unscaledDeltaTime);
        }
    }

    private void AnimateSingleplayerTab()
    {
        if (_rotatingTPiece != null && _panels[(int)TabID.Singleplayer].activeSelf)
            _rotatingTPiece.localEulerAngles = new Vector3(0f, 0f, Time.unscaledTime * 45f);

        if (_playPromptText != null && _panels[(int)TabID.Singleplayer].activeSelf)
        {
            // Gentle breathing on the "PRESS ENTER" prompt.
            float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
            Color c = _playPromptText.color;
            _playPromptText.color = new Color(c.r, c.g, c.b, a);
        }
    }

    private void AnimateBackground()
    {
        // Drift each piece downward; wrap back to top with a new x / color once
        // it leaves the screen. Speed / rotate-speed were seeded once at spawn
        // for parallax-ish variation.
        Rect screen = ((RectTransform)transform).rect;
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
                    img.color = new Color(p.color.r, p.color.g, p.color.b, 0.22f);
            }
            p.root.anchoredPosition = pos;
            p.root.localEulerAngles  = new Vector3(0f, 0f, Time.unscaledTime * p.rotateSpeed);
        }
    }

    // Helper: tiny MonoBehaviour used only to cache each tab's baseline anchor
    // position, so AnimateTabs can bob around a stable reference point.
    private class TabBaseline : MonoBehaviour { public Vector2 basePos; }

    // ── Canvas / UI construction ─────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        BuildBackground();
        BuildFallingPieces(count: 11);
        BuildTitleBar();
        BuildTabBar();
        BuildAllPanels();
        BuildFooter();
    }

    private void BuildBackground()
    {
        // Layer 1: replaceable placeholder image (or procedural navy fill).
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(transform, false);
        Image bgImg = bg.AddComponent<Image>();
        if (backgroundImage != null)
        {
            bgImg.sprite        = backgroundImage;
            bgImg.preserveAspect = false;
            bgImg.color          = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.035f, 0.04f, 0.09f, 1f); // deep navy
        }
        bgImg.raycastTarget = false;
        StretchFull(bg.GetComponent<RectTransform>());

        // Layer 2: vertical gradient overlay (dark top → purple bottom). Baked
        // once into a small texture so we don't ship a shader.
        GameObject grad = new GameObject("Gradient");
        grad.transform.SetParent(transform, false);
        Image gImg = grad.AddComponent<Image>();
        gImg.sprite        = BuildGradientSprite();
        gImg.preserveAspect = false;
        gImg.color         = new Color(1f, 1f, 1f, 0.55f);
        gImg.raycastTarget = false;
        StretchFull(grad.GetComponent<RectTransform>());

        // Layer 3: grid pattern overlay - gives the impression of a faint
        // tetris playfield behind everything.
        GameObject grid = new GameObject("GridOverlay");
        grid.transform.SetParent(transform, false);
        Image grImg = grid.AddComponent<Image>();
        grImg.sprite        = BuildGridSprite();
        grImg.type          = Image.Type.Tiled;
        grImg.color         = new Color(1f, 1f, 1f, 0.05f);
        grImg.raycastTarget = false;
        StretchFull(grid.GetComponent<RectTransform>());
    }

    private void BuildFallingPieces(int count)
    {
        // Seed the layer behind everything - parented to transform, sandwiched
        // above the gradient/grid overlay but below the title/tabs/panels.
        // Construct with RectTransform from the start; it's the safer pattern
        // for UI containers that don't also host a Graphic component.
        GameObject layer = new GameObject("FallingPieces", typeof(RectTransform));
        layer.transform.SetParent(transform, false);
        StretchFull((RectTransform)layer.transform);

        Rect screen = ((RectTransform)transform).rect;
        for (int i = 0; i < count; i++)
        {
            var piece = BuildOneFallingPiece(layer.transform);
            Vector2 pos = new Vector2(
                Random.Range(-screen.width * 0.48f, screen.width * 0.48f),
                Random.Range(-screen.height * 0.5f, screen.height * 0.5f));
            piece.root.anchoredPosition = pos;
            piece.speed       = Random.Range(35f, 95f);
            piece.rotateSpeed = Random.Range(-25f, 25f);
            _bgPieces.Add(piece);
        }
    }

    private FallingPiece BuildOneFallingPiece(Transform parent)
    {
        // Pick a random tetromino shape; draw it with 4 rounded squares of a
        // chosen color. Low alpha so it's visibly behind the content.
        Vector2Int[][] shapes = Tetrominoes;
        Vector2Int[] cells = shapes[Random.Range(0, shapes.Length)];

        GameObject root = new GameObject("Piece", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot     = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(220f, 220f); // doesn't matter much; cells use absolute offsets

        Color c = RandomPieceColor();
        const float cellSize = 48f;
        foreach (var cell in cells)
        {
            GameObject cellGo = new GameObject("cell");
            cellGo.transform.SetParent(root.transform, false);
            Image img = cellGo.AddComponent<Image>();
            img.sprite = UIRoundedSprite.Default;
            img.type   = Image.Type.Sliced;
            img.color  = new Color(c.r, c.g, c.b, 0.22f);
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

    // Canonical tetromino cell offsets (centered on origin-ish).
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
        // Palette roughly matches the classic tetromino colors.
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

    // ── Title bar ────────────────────────────────────────────────────────────

    private void BuildTitleBar()
    {
        // "BLOCK BATTLE" across the top. Subtitle sits safely ABOVE the tab
        // column - the old layout had them overlapping because the horizontal
        // tabs landed at y=-215 and the subtitle ended at y=-230.
        _titleText = CreateLabel(transform, "BLOCK BATTLE",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -40f), size: new Vector2(1700f, 130f),
            fontSize: 110f, color: new Color(0f, 0.85f, 1f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);

        CreateLabel(transform, "by Cameron Reynes",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -180f), size: new Vector2(1700f, 40f),
            fontSize: 26f, color: new Color(1f, 1f, 1f, 0.65f), style: FontStyles.Italic,
            alignment: TextAlignmentOptions.Center);
    }

    // ── Tab bar ──────────────────────────────────────────────────────────────

    private void BuildTabBar()
    {
        // Five big stacked cards on the LEFT THIRD of the screen. Each tab is a
        // pure-container RectTransform that holds children in this order:
        //   0. outline Image   (slightly larger, rendered behind → only rim shows)
        //   1. bg Image        (colored rounded card, raycastTarget=true)
        //   2. text label      (left-aligned, menu-list feel)
        //   3. lock icon       (multiplayer only - procedural, not emoji)
        //
        // Button + TabHoverTracker live on the container; events bubble up from
        // the bg Image so clicks and hovers register on the whole card.

        for (int i = 0; i < TabLabels.Length; i++)
        {
            // `y` is the tab's left-center, since pivot is (0, 0.5).
            float y = TabColTopY - i * (TabHeight + TabGap) - TabHeight * 0.5f;

            GameObject tab = new GameObject($"Tab_{TabLabels[i]}", typeof(RectTransform));
            tab.transform.SetParent(transform, false);

            RectTransform rt = (RectTransform)tab.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(TabColX, y);
            rt.sizeDelta        = new Vector2(TabWidth, TabHeight);

            TabBaseline bl = tab.AddComponent<TabBaseline>();
            bl.basePos = rt.anchoredPosition;

            // Child 0 - outline (slightly larger than the tab, initially invisible).
            GameObject outlineGo = new GameObject("outline");
            outlineGo.transform.SetParent(tab.transform, false);
            Image outline = outlineGo.AddComponent<Image>();
            outline.sprite        = UIRoundedSprite.Default;
            outline.type          = Image.Type.Sliced;
            outline.color         = new Color(1f, 1f, 1f, 0f);
            outline.raycastTarget = false;
            RectTransform ort = outlineGo.GetComponent<RectTransform>();
            ort.anchorMin = new Vector2(0.5f, 0.5f);
            ort.anchorMax = new Vector2(0.5f, 0.5f);
            ort.pivot     = new Vector2(0.5f, 0.5f);
            ort.anchoredPosition = Vector2.zero;
            ort.sizeDelta = new Vector2(TabWidth + TabOutlineInset * 2f, TabHeight + TabOutlineInset * 2f);
            _tabOutlines.Add(outline);

            // Child 1 - bg (the colored rounded card, fills the container).
            GameObject bgGo = new GameObject("bg");
            bgGo.transform.SetParent(tab.transform, false);
            Image bg = bgGo.AddComponent<Image>();
            bg.sprite        = UIRoundedSprite.Default;
            bg.type          = Image.Type.Sliced;
            bg.color         = TabColors[i];
            bg.raycastTarget = true; // clicks / hovers land here and bubble up
            RectTransform brt = bgGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            _tabBgs.Add(bg);

            int capture = i;
            Button btn = tab.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => SelectTab((TabID)capture));

            _tabHovers.Add(tab.AddComponent<TabHoverTracker>());
            _tabButtons.Add(tab);
            _tabRects.Add(rt);

            // Child 2 - label (left-aligned, indented so the lock icon fits).
            TextMeshProUGUI lbl = CreateLabel(tab.transform, TabLabels[i],
                anchor: new Vector2(0f, 0.5f), pivot: new Vector2(0f, 0.5f),
                anchoredPos: new Vector2(32f, 0f),
                size: new Vector2(TabWidth - 100f, TabHeight - 12f),
                fontSize: 32f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Left);
            _tabLabels.Add(lbl);

            // Child 3 - procedural lock for the multiplayer tab. Drawn from 4
            // rounded rects so the default TMP font (which lacks 🔒) isn't an issue.
            if ((TabID)i == TabID.Multiplayer)
            {
                GameObject lockIcon = BuildLockIcon(tab.transform, 52f, new Color(1f, 1f, 1f, 0.9f));
                RectTransform lrt = (RectTransform)lockIcon.transform;
                lrt.anchorMin = new Vector2(1f, 0.5f);
                lrt.anchorMax = new Vector2(1f, 0.5f);
                lrt.pivot     = new Vector2(1f, 0.5f);
                lrt.anchoredPosition = new Vector2(-22f, 0f);
            }
        }
    }

    // ── Panels ───────────────────────────────────────────────────────────────

    private void BuildAllPanels()
    {
        // Order MUST match TabID. The list is indexed by (int)tab.
        _panels.Add(BuildSingleplayerPanel());
        _panels.Add(BuildMultiplayerPanel());
        _panels.Add(BuildControlsPanel());
        _panels.Add(BuildLeaderboardPanel());
        _panels.Add(BuildStatsPanel());
        _panels.Add(BuildAboutPanel());
        for (int i = 0; i < _panels.Count; i++) _panels[i].SetActive(false);
    }

    /// <summary>Rounded rect anchored to the top-right, sized for the right
    /// two-thirds of the screen (the tab column owns the left third).</summary>
    private GameObject CreatePanelShell(string name, Color accentStripe)
    {
        GameObject p = new GameObject(name);
        p.transform.SetParent(transform, false);
        Image img = p.AddComponent<Image>();
        img.sprite = UIRoundedSprite.Default;
        img.type   = Image.Type.Sliced;
        img.color  = new Color(0.05f, 0.06f, 0.10f, 0.93f);
        img.raycastTarget = false;
        RectTransform rt = p.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-PanelMarginR, PanelTopY);
        rt.sizeDelta        = new Vector2(PanelWidth, PanelHeight);

        // Thin accent stripe along the top edge - colored per tab.
        GameObject stripe = new GameObject("AccentStripe");
        stripe.transform.SetParent(p.transform, false);
        Image sImg = stripe.AddComponent<Image>();
        sImg.sprite = UIRoundedSprite.Default;
        sImg.type   = Image.Type.Sliced;
        sImg.color  = accentStripe;
        sImg.raycastTarget = false;
        RectTransform sRt = stripe.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot     = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -10f);
        sRt.sizeDelta        = new Vector2(-40f, 8f);

        return p;
    }

    // ── Singleplayer panel ───────────────────────────────────────────────────

    private GameObject BuildSingleplayerPanel()
    {
        GameObject p = CreatePanelShell("Panel_Singleplayer", TabColors[(int)TabID.Singleplayer]);

        CreateLabel(p.transform, "READY TO STACK?",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -70f), size: new Vector2(1150f, 90f),
            fontSize: 64f, color: new Color(0f, 0.9f, 1f), style: FontStyles.Bold);

        CreateLabel(p.transform, "CLASSIC BLOCK BATTLE - CLEAR LINES, CLIMB LEVELS, CHASE A NEW HIGH SCORE",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -170f), size: new Vector2(1150f, 45f),
            fontSize: 24f, color: new Color(1f, 1f, 1f, 0.8f), style: FontStyles.Normal);

        // Rotating T-piece preview - a simple 4-cell arrangement that spins.
        GameObject spin = new GameObject("SpinT", typeof(RectTransform));
        spin.transform.SetParent(p.transform, false);
        RectTransform spinRt = (RectTransform)spin.transform;
        spinRt.anchorMin = new Vector2(0.5f, 0.5f);
        spinRt.anchorMax = new Vector2(0.5f, 0.5f);
        spinRt.pivot     = new Vector2(0.5f, 0.5f);
        spinRt.anchoredPosition = new Vector2(0f, -40f);
        spinRt.sizeDelta        = new Vector2(240f, 240f);
        _rotatingTPiece = spinRt;

        // T shape, centered.
        const float cell = 54f;
        Vector2Int[] tShape = { new(-1, 0), new(0, 0), new(1, 0), new(0, 1) };
        foreach (var c in tShape)
        {
            GameObject blk = new GameObject("TCell");
            blk.transform.SetParent(spin.transform, false);
            Image bImg = blk.AddComponent<Image>();
            bImg.sprite = UIRoundedSprite.Default;
            bImg.type   = Image.Type.Sliced;
            bImg.color  = TabColors[(int)TabID.About]; // T-piece purple
            bImg.raycastTarget = false;
            RectTransform brt = blk.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot     = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(cell - 4f, cell - 4f);
            brt.anchoredPosition = new Vector2(c.x * cell, c.y * cell);
        }

        // Big flashing PLAY prompt.
        _playPromptText = CreateLabel(p.transform, "▶  PRESS ENTER TO PLAY  ◀",
            anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 60f), size: new Vector2(1150f, 80f),
            fontSize: 44f, color: new Color(1f, 0.95f, 0.2f), style: FontStyles.Bold);

        return p;
    }

    // ── Multiplayer panel (locked) ───────────────────────────────────────────

    private GameObject BuildMultiplayerPanel()
    {
        GameObject p = CreatePanelShell("Panel_Multiplayer", new Color(0.4f, 0.4f, 0.45f));

        // Big procedural padlock - previously a 🔒 emoji, which rendered as
        // a tofu box on systems whose TMP atlas lacks emoji glyphs.
        GameObject bigLock = BuildLockIcon(p.transform, 180f, new Color(0.78f, 0.78f, 0.82f, 1f));
        RectTransform blrt = (RectTransform)bigLock.transform;
        blrt.anchorMin = new Vector2(0.5f, 1f);
        blrt.anchorMax = new Vector2(0.5f, 1f);
        blrt.pivot     = new Vector2(0.5f, 1f);
        blrt.anchoredPosition = new Vector2(0f, -90f);

        CreateLabel(p.transform, "LOCKED",
            anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(0f, 30f), size: new Vector2(1150f, 100f),
            fontSize: 88f, color: new Color(1f, 1f, 1f, 0.45f), style: FontStyles.Bold);

        CreateLabel(p.transform, "MULTIPLAYER IS UNDER CONSTRUCTION",
            anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(0f, -60f), size: new Vector2(1150f, 50f),
            fontSize: 32f, color: new Color(1f, 0.85f, 0.4f), style: FontStyles.Bold);

        CreateLabel(p.transform, "1v1 Tetris™ Friends inspired battle with garbage sends, KO counters, and real-time chaos",
            anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(0f, -115f), size: new Vector2(1150f, 40f),
            fontSize: 22f, color: new Color(1f, 1f, 1f, 0.55f), style: FontStyles.Italic);

        return p;
    }

    // ── Controls panel ───────────────────────────────────────────────────────

    private GameObject BuildControlsPanel()
    {
        GameObject p = CreatePanelShell("Panel_Controls", TabColors[(int)TabID.Controls]);

        CreateLabel(p.transform, "CONTROLS",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -60f), size: new Vector2(1150f, 80f),
            fontSize: 50f, color: TabColors[(int)TabID.Controls], style: FontStyles.Bold);

        // Subtitle sells the context: this build targets a real arcade cabinet
        // so only arcade bindings are surfaced. Keyboard mappings still exist
        // in TetrixControls for dev work but there's no reason to clutter the
        // shipped menu with them.
        CreateLabel(p.transform, "ARCADE CABINET - STICK + BUTTONS",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -125f), size: new Vector2(1150f, 40f),
            fontSize: 24f, color: new Color(1f, 1f, 1f, 0.65f), style: FontStyles.Italic);

        // Arcade-only bindings. Mirrors the joystick half of
        // TetrixControls.inputactions - keep in sync if buttons get rewired.
        // Order roughly follows frequency-of-use: movement first, then the
        // occasional buttons, with CONFIRM last as the menu-only action.
        // Using a tuple array instead of string[,] because C# 9 tuple syntax
        // reads better and you can index named members (.action/.binding)
        // rather than magic column indices.
        (string action, string binding)[] rows =
        {
            ("MOVE LEFT",  "Stick LEFT"),
            ("MOVE RIGHT", "Stick RIGHT"),
            ("SOFT DROP",  "Stick DOWN"),
            ("HOLD",       "Stick UP"),
            ("ROTATE CW",  "Button 11"),
            ("HARD DROP",  "Button 12"),
            ("CONFIRM",    "Button A"),
        };

        // Two-column layout centered in the 1260-wide panel. The action column
        // sits left of center, the binding column right of center, with a
        // comfortable gutter. Row step is a little taller than the old 3-col
        // version because we have fewer rows - lets the text breathe and
        // keeps the section feeling deliberate instead of sparse.
        const float actionColX = -220f;
        const float bindColX   =  220f;
        const float headerY    = -200f;
        const float rowStartY  = -260f;
        const float rowStep    =  58f;
        const float zebraWidth = 1020f;

        CreateLabel(p.transform, "ACTION",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(actionColX, headerY), size: new Vector2(400f, 40f),
            fontSize: 26f, color: new Color(1f, 1f, 1f, 0.7f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left);

        CreateLabel(p.transform, "ARCADE CONTROL",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(bindColX, headerY), size: new Vector2(400f, 40f),
            fontSize: 26f, color: TabColors[(int)TabID.Controls], style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left);

        for (int i = 0; i < rows.Length; i++)
        {
            float y = rowStartY - i * rowStep;

            // Alternating row background - subtle zebra for readability. Pivot
            // y=0.5 so the zebra centers on `y`, while labels (pivot y=1) sit
            // at y + rowStep/2 so their vertical middle lines up with `y`.
            // Same trick the leaderboard uses.
            if (i % 2 == 0)
            {
                GameObject zebra = new GameObject("zebra");
                zebra.transform.SetParent(p.transform, false);
                Image zimg = zebra.AddComponent<Image>();
                zimg.sprite = UIRoundedSprite.Default;
                zimg.type   = Image.Type.Sliced;
                zimg.color  = new Color(1f, 1f, 1f, 0.04f);
                zimg.raycastTarget = false;
                RectTransform zrt = zebra.GetComponent<RectTransform>();
                zrt.anchorMin = new Vector2(0.5f, 1f);
                zrt.anchorMax = new Vector2(0.5f, 1f);
                zrt.pivot     = new Vector2(0.5f, 0.5f);
                zrt.anchoredPosition = new Vector2(0f, y);
                zrt.sizeDelta        = new Vector2(zebraWidth, rowStep - 6f);
            }

            float textY = y + rowStep * 0.5f;

            CreateLabel(p.transform, rows[i].action,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(actionColX, textY), size: new Vector2(400f, rowStep),
                fontSize: 26f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Left);

            CreateLabel(p.transform, rows[i].binding,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(bindColX, textY), size: new Vector2(400f, rowStep),
                fontSize: 26f, color: new Color(1f, 0.85f, 0.5f), style: FontStyles.Normal,
                alignment: TextAlignmentOptions.Left);
        }

        return p;
    }

    // ── Leaderboard panel ────────────────────────────────────────────────────

    private readonly List<TextMeshProUGUI> _lbRank  = new();
    private readonly List<TextMeshProUGUI> _lbName  = new();
    private readonly List<TextMeshProUGUI> _lbScore = new();
    private readonly List<TextMeshProUGUI> _lbLines = new();
    private readonly List<TextMeshProUGUI> _lbLevel = new();

    private GameObject BuildLeaderboardPanel()
    {
        GameObject p = CreatePanelShell("Panel_Leaderboard", TabColors[(int)TabID.Leaderboard]);

        CreateLabel(p.transform, "LEADERBOARD - TOP 10",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -60f), size: new Vector2(1150f, 80f),
            fontSize: 50f, color: TabColors[(int)TabID.Leaderboard], style: FontStyles.Bold);

        // Column X positions tuned for the 1260-wide panel.
        const float rankX  = -520f;
        const float nameX  = -320f;
        const float scoreX =    0f;
        const float levelX =  320f;
        const float linesX =  490f;
        const float headerY    = -160f;
        const float rowStartY  = -215f;
        const float rowStep    =  40f;
        const float zebraWidth = 1140f;

        string[] headers = { "#", "NAME", "SCORE", "LEVEL", "LINES" };
        float[]  xs      = { rankX, nameX, scoreX, levelX, linesX };
        for (int i = 0; i < headers.Length; i++)
            CreateLabel(p.transform, headers[i],
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(xs[i], headerY), size: new Vector2(200f, 40f),
                fontSize: 22f, color: new Color(1f, 1f, 1f, 0.65f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center);

        for (int i = 0; i < Leaderboard.MaxEntries; i++)
        {
            // `y` is the CENTER of the row. The zebra (pivot 0.5,0.5) is placed
            // at y directly. Text labels (pivot 0.5,1) are placed at y + rowStep/2
            // so their vertical CENTER lines up with y - matching the Controls
            // panel pattern. Before this fix, text used `y` as its TOP which
            // dropped rows a half-row below their zebra stripe.
            float y = rowStartY - i * rowStep;
            float textY = y + rowStep * 0.5f;

            if (i % 2 == 0)
            {
                GameObject zebra = new GameObject("zebra");
                zebra.transform.SetParent(p.transform, false);
                Image zimg = zebra.AddComponent<Image>();
                zimg.sprite = UIRoundedSprite.Default;
                zimg.type   = Image.Type.Sliced;
                zimg.color  = new Color(1f, 1f, 1f, 0.03f);
                zimg.raycastTarget = false;
                RectTransform zrt = zebra.GetComponent<RectTransform>();
                zrt.anchorMin = new Vector2(0.5f, 1f);
                zrt.anchorMax = new Vector2(0.5f, 1f);
                zrt.pivot     = new Vector2(0.5f, 0.5f);
                zrt.anchoredPosition = new Vector2(0f, y);
                zrt.sizeDelta        = new Vector2(zebraWidth, rowStep - 4f);
            }

            _lbRank.Add(CreateLabel(p.transform, $"{i + 1}.",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(rankX, textY), size: new Vector2(100f, rowStep),
                fontSize: 22f, color: new Color(1f, 0.85f, 0.13f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));

            _lbName.Add(CreateLabel(p.transform, "---",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(nameX, textY), size: new Vector2(240f, rowStep),
                fontSize: 22f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));

            _lbScore.Add(CreateLabel(p.transform, "---",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(scoreX, textY), size: new Vector2(240f, rowStep),
                fontSize: 22f, color: new Color(0.5f, 0.9f, 1f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center));

            _lbLevel.Add(CreateLabel(p.transform, "---",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(levelX, textY), size: new Vector2(180f, rowStep),
                fontSize: 22f, color: new Color(0.85f, 0.85f, 0.85f), style: FontStyles.Normal,
                alignment: TextAlignmentOptions.Center));

            _lbLines.Add(CreateLabel(p.transform, "---",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(linesX, textY), size: new Vector2(180f, rowStep),
                fontSize: 22f, color: new Color(0.85f, 0.85f, 0.85f), style: FontStyles.Normal,
                alignment: TextAlignmentOptions.Center));
        }

        return p;
    }

    private void RefreshLeaderboardPanel()
    {
        var entries = Leaderboard.Load();
        for (int i = 0; i < Leaderboard.MaxEntries; i++)
        {
            if (i < entries.Count)
            {
                _lbRank[i].text  = $"{i + 1}.";
                _lbName[i].text  = entries[i].name;
                _lbScore[i].text = entries[i].score.ToString("N0");
                _lbLevel[i].text = entries[i].level.ToString();
                _lbLines[i].text = entries[i].lines.ToString();
            }
            else
            {
                _lbRank[i].text  = $"{i + 1}.";
                _lbName[i].text  = "---";
                _lbScore[i].text = "---";
                _lbLevel[i].text = "---";
                _lbLines[i].text = "---";
            }
        }
    }

    // ── Stats panel ──────────────────────────────────────────────────────────
    //
    // Two-column lifetime stats display backed by PlayerStats. The left column
    // is "CLEARS" (line-clear breakdown by type), the right column is "RECORDS
    // & TOTALS" (per-run records + lifetime totals). Each row is rebuilt via
    // RefreshStatsPanel whenever the Stats tab is selected, so values are
    // always live — important because the player will jump from death screen
    // → main menu and expect the new game's totals to be reflected immediately.

    // Cached label refs for live refresh. Order matches the spec the user gave:
    // lines, singles, doubles, triples, tetris, mini t-spin, t-spin double,
    // t-spin triple, perfect clears.
    private TextMeshProUGUI _statLines;
    private TextMeshProUGUI _statSingles;
    private TextMeshProUGUI _statDoubles;
    private TextMeshProUGUI _statTriples;
    private TextMeshProUGUI _statTetris;
    private TextMeshProUGUI _statMiniTSpin;
    private TextMeshProUGUI _statTSpinDouble;
    private TextMeshProUGUI _statTSpinTriple;
    private TextMeshProUGUI _statPerfectClears;

    // Right column.
    private TextMeshProUGUI _statHighScore;
    private TextMeshProUGUI _statHighLevel;
    private TextMeshProUGUI _statHighCombo;
    private TextMeshProUGUI _statLongestSurvival;
    private TextMeshProUGUI _statTotalGames;
    private TextMeshProUGUI _statTotalMinutes;
    private TextMeshProUGUI _statTotalLevels;

    private GameObject BuildStatsPanel()
    {
        GameObject p = CreatePanelShell("Panel_Stats", TabColors[(int)TabID.Stats]);

        CreateLabel(p.transform, "LIFETIME STATS",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -60f), size: new Vector2(1150f, 80f),
            fontSize: 50f, color: TabColors[(int)TabID.Stats], style: FontStyles.Bold);

        // Two-column rows. Each column uses the same row-height/zebra pattern
        // as the Controls / Leaderboard panels for visual consistency.
        // Layout knobs — pulled out so all rows share them and tweaks land in
        // one place if a new stat is added later.
        const float colHeaderY = -150f;
        const float rowStartY  = -210f;
        const float rowStep    =  44f;
        const float colCenterL = -310f; // center of the LEFT column
        const float colCenterR =  310f; // center of the RIGHT column
        const float labelW     =  250f; // left-aligned key
        const float valueW     =  220f; // right-aligned value
        const float zebraWidth =  560f;

        // Column headers.
        CreateLabel(p.transform, "CLEARS",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(colCenterL, colHeaderY), size: new Vector2(zebraWidth, 36f),
            fontSize: 26f, color: TabColors[(int)TabID.Stats], style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);
        CreateLabel(p.transform, "RECORDS & TOTALS",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(colCenterR, colHeaderY), size: new Vector2(zebraWidth, 36f),
            fontSize: 26f, color: TabColors[(int)TabID.Stats], style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);

        // ── Left column rows (clear-type counts) ──
        // Order matches the user-specified spec exactly. Mini-t-spin sits
        // between Tetris and the larger T-spin variants, matching how players
        // intuitively rank them by impressiveness.
        (string label, System.Action<TextMeshProUGUI> assign)[] leftRows =
        {
            ("LINES CLEARED",   t => _statLines          = t),
            ("SINGLES",         t => _statSingles        = t),
            ("DOUBLES",         t => _statDoubles        = t),
            ("TRIPLES",         t => _statTriples        = t),
            ("TETRIS",          t => _statTetris         = t),
            ("MINI T-SPIN",     t => _statMiniTSpin      = t),
            ("T-SPIN DOUBLE",   t => _statTSpinDouble    = t),
            ("T-SPIN TRIPLE",   t => _statTSpinTriple    = t),
            ("PERFECT CLEARS",  t => _statPerfectClears  = t),
        };
        BuildStatColumn(p.transform, leftRows, colCenterL, rowStartY, rowStep, labelW, valueW, zebraWidth);

        // ── Right column rows (records + totals) ──
        (string label, System.Action<TextMeshProUGUI> assign)[] rightRows =
        {
            ("HIGHEST SCORE",     t => _statHighScore       = t),
            ("HIGHEST LEVEL",     t => _statHighLevel       = t),
            ("HIGHEST COMBO",     t => _statHighCombo       = t),
            ("LONGEST SURVIVAL",  t => _statLongestSurvival = t),
            ("TOTAL GAMES",       t => _statTotalGames      = t),
            ("TOTAL TIME PLAYED", t => _statTotalMinutes    = t),
            ("TOTAL LEVELS",      t => _statTotalLevels     = t),
        };
        BuildStatColumn(p.transform, rightRows, colCenterR, rowStartY, rowStep, labelW, valueW, zebraWidth);

        return p;
    }

    /// <summary>
    /// Lays out one column of stat rows: alternating zebra background, key
    /// label on the left, value label on the right. The caller's `assign`
    /// callback is given the value TMP so the per-row stat field can be
    /// captured for later refresh.
    /// </summary>
    private void BuildStatColumn(Transform parent,
        (string label, System.Action<TextMeshProUGUI> assign)[] rows,
        float colCenterX, float rowStartY, float rowStep,
        float labelW, float valueW, float zebraWidth)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            float y = rowStartY - i * rowStep;
            float textY = y + rowStep * 0.5f;

            if (i % 2 == 0)
            {
                GameObject zebra = new GameObject("zebra");
                zebra.transform.SetParent(parent, false);
                Image zimg = zebra.AddComponent<Image>();
                zimg.sprite = UIRoundedSprite.Default;
                zimg.type   = Image.Type.Sliced;
                zimg.color  = new Color(1f, 1f, 1f, 0.04f);
                zimg.raycastTarget = false;
                RectTransform zrt = zebra.GetComponent<RectTransform>();
                zrt.anchorMin = new Vector2(0.5f, 1f);
                zrt.anchorMax = new Vector2(0.5f, 1f);
                zrt.pivot     = new Vector2(0.5f, 0.5f);
                zrt.anchoredPosition = new Vector2(colCenterX, y);
                zrt.sizeDelta        = new Vector2(zebraWidth, rowStep - 6f);
            }

            // Label sits at the left of the column. Value sits at the right.
            // Both use the same baseline `textY` so they read as a single row.
            float labelX = colCenterX - zebraWidth * 0.5f + labelW * 0.5f + 12f;
            float valueX = colCenterX + zebraWidth * 0.5f - valueW * 0.5f - 12f;

            CreateLabel(parent, rows[i].label,
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(labelX, textY), size: new Vector2(labelW, rowStep),
                fontSize: 22f, color: new Color(1f, 1f, 1f, 0.78f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Left);

            TextMeshProUGUI value = CreateLabel(parent, "0",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(valueX, textY), size: new Vector2(valueW, rowStep),
                fontSize: 22f, color: new Color(0.5f, 0.9f, 1f), style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Right);

            rows[i].assign(value);
        }
    }

    private void RefreshStatsPanel()
    {
        // Re-pull every value from PlayerStats so a game finishing right
        // before the player reaches this tab shows up immediately.
        var d = PlayerStats.Get();

        _statLines.text          = d.linesCleared.ToString("N0");
        _statSingles.text        = d.singles.ToString("N0");
        _statDoubles.text        = d.doubles.ToString("N0");
        _statTriples.text        = d.triples.ToString("N0");
        _statTetris.text         = d.tetris.ToString("N0");
        _statMiniTSpin.text      = d.miniTSpin.ToString("N0");
        _statTSpinDouble.text    = d.tSpinDouble.ToString("N0");
        _statTSpinTriple.text    = d.tSpinTriple.ToString("N0");
        _statPerfectClears.text  = d.perfectClears.ToString("N0");

        _statHighScore.text       = d.highestScore.ToString("N0");
        _statHighLevel.text       = d.highestLevel.ToString();
        _statHighCombo.text       = d.highestComboStreak.ToString();
        _statLongestSurvival.text = PlayerStats.FormatSeconds(d.longestSurvivalSeconds);
        _statTotalGames.text      = d.totalGamesPlayed.ToString("N0");
        _statTotalMinutes.text    = PlayerStats.FormatMinutes(d.totalMinutesPlayed);
        _statTotalLevels.text     = d.totalLevelsPassed.ToString("N0");
    }

    // ── About panel ──────────────────────────────────────────────────────────
    //
    // Three-section layout:
    //   • Top-middle:   title + a paragraph describing what Block Battle is.
    //   • Bottom-left:  "CREATED BY" header + Cameron Reynes + four social rows.
    //   • Bottom-right: "SPECIAL THANKS TO" header + Grant Harvey + two rows.
    //
    // Each social row renders as [PNG icon] [LABEL] [handle]. The PNGs live
    // under Assets/Menu/Resources/SocialIcons/ — being inside a Resources
    // folder is the only sanctioned way to load assets by string at runtime
    // in a built Unity player, so we keep them there and Resources.Load them
    // on first use. The procedural-badge fallback in BuildSocialIcon below
    // is preserved as a safety net for the case where a load fails (e.g.
    // someone moves the folder out of Resources at some point).

    /// <summary>Hint for the procedural fallback in BuildSocialIcon when a
    /// row's iconSprite is null. Once the Resources PNGs are wired up this
    /// fallback rarely runs, but it still kicks in cleanly if a load fails.
    /// Instagram gets a small camera-lens motif; everything else falls
    /// through to a brand-tinted first-letter badge.</summary>
    private enum SocialIcon { Email, Instagram, Discord, YouTube, GitHub, LinkedIn, Twitter, Generic }

    /// <summary>One credit row. Used to be Inspector-driven (one [SerializeField]
    /// per platform) but the four PNGs we ship are now wired up automatically
    /// via Resources.Load, so the rows are constructed in code where the
    /// content lives next to the section it belongs to. Tint colors the bold
    /// LABEL text and is also reused by the procedural-badge fallback when
    /// no iconSprite is available.</summary>
    private class SocialLink
    {
        public SocialIcon icon = SocialIcon.Generic;
        public Sprite     iconSprite;
        public string     label  = "";
        public string     handle = "";
        public Color      tint   = Color.white;
    }

    // Brand-ish tints for the bold LABEL text. Restrained on purpose so they
    // don't fight the rest of the menu's tetromino palette. GitHub is a cool
    // off-white because the actual GitHub logo is monochrome and a dark gray
    // tint would make the LABEL text disappear against the dark panel BG.
    private static readonly Color TintEmail     = new(0.95f, 0.55f, 0.25f);
    private static readonly Color TintInstagram = new(0.91f, 0.27f, 0.53f);
    private static readonly Color TintGithub    = new(0.78f, 0.78f, 0.85f);
    private static readonly Color TintLinkedin  = new(0.30f, 0.62f, 0.92f);

    // Sprite handles cached at first About-panel build. Static so a hot
    // reload that re-runs Awake doesn't pay for the load again, and lazy
    // (rather than fired from Awake) so a player who never opens the About
    // tab doesn't pay for them at all.
    private static Sprite _iconEmail, _iconInstagram, _iconGithub, _iconLinkedin;
    private static bool   _iconsLoaded;

    private static void EnsureSocialIcons()
    {
        if (_iconsLoaded) return;
        _iconsLoaded = true;
        // The PNG metas were imported with spriteMode=Multiple (the default
        // when the importer auto-carves a single sub-sprite from a non-power-
        // of-two source). Resources.Load<Sprite>(path) returns null on those
        // because there's no "main" sprite asset — only sub-sprites. LoadAll
        // works for both Single and Multiple, so it's the safer call and
        // survives any future re-import that flips the importer mode.
        Sprite Pick(string name)
        {
            var all = Resources.LoadAll<Sprite>("SocialIcons/" + name);
            return (all != null && all.Length > 0) ? all[0] : null;
        }
        _iconEmail     = Pick("gmail");
        _iconInstagram = Pick("instagram");
        _iconGithub    = Pick("github");
        _iconLinkedin  = Pick("linkedin");
    }

    // ── Layout knobs ──
    // Y is measured from the panel's TOP (negative = down). X for the LEFT
    // column is from the panel's left edge (positive = right); for the RIGHT
    // column it's measured from the right edge (negative = left). Both
    // columns share the same row strip width so the icon, label, and handle
    // line up across rows within their own column.
    //
    // Sizing notes from earlier passes: AboutLabelWidth has to be wider than
    // the longest LABEL text rendered at the row font size, otherwise TMP
    // (which has wrapping disabled in CreateLabel) overflows the box and
    // the label visually crashes into the handle. "INSTAGRAM" at 24pt bold
    // measures roughly ~140px, so 180 leaves a comfortable buffer.
    // AboutHandleWidth has to clear the longest handle string we render —
    // "grantharvey616@gmail.com" at 24pt is ~290px, fits in 320 with room.
    // Strip width × 2 + 2*inset must stay under PanelWidth (1260) — current
    // strip is 576 (52+14+180+10+320), giving 48px between the columns.
    private const float AboutTitleY         =  -60f;
    private const float AboutDescY          = -150f;
    private const float AboutDescHeight     =  240f;
    private const float AboutDescWidth      = 1180f;

    private const float AboutSectionHeaderY = -395f;   // "CREATED BY" / "SPECIAL THANKS TO"
    private const float AboutSectionNameY   = -440f;   // name + role line just below the header
    private const float AboutCreditFirstY   = -510f;   // first row's icon-center y
    private const float AboutCreditStep     =   58f;   // gap between row centers
    private const float AboutSectionInsetX  =   30f;   // horizontal padding inside the panel
    private const float AboutIconSize       =   52f;
    private const float AboutLabelWidth     =  180f;
    private const float AboutHandleWidth    =  320f;

    // Font sizes. Pulled out as named constants because earlier they were
    // sprinkled inline and any tune-up turned into a multi-spot edit. Pumped
    // up from the original sizing pass — the panel is 1260×720 and the
    // earlier ~20pt sizing left a lot of dead space.
    private const float AboutDescFontSize   = 28f;
    private const float AboutHeaderFontSize = 24f;
    private const float AboutNameFontSize   = 28f;
    private const float AboutRowFontSize    = 24f;

    private GameObject BuildAboutPanel()
    {
        EnsureSocialIcons();

        GameObject p = CreatePanelShell("Panel_About", TabColors[(int)TabID.About]);

        // ── Title ──
        CreateLabel(p.transform, "ABOUT BLOCK BATTLE",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, AboutTitleY), size: new Vector2(1150f, 80f),
            fontSize: 50f, color: TabColors[(int)TabID.About], style: FontStyles.Bold);

        // ── Top-middle blurb ──
        // CreateLabel disables word wrapping by default since most labels in
        // this menu are single-line strips. The about paragraph is the one
        // place that needs wrapping, so we flip it back on after the fact
        // rather than threading another parameter through every CreateLabel
        // call elsewhere.
        var desc = CreateLabel(p.transform,
            "I developed Block Battle during my junior and senior years of undergraduate as a fun personal project. " +
            "It was meant to replicate the deprecated 'Tetris Friends' game. Multiplayer (1v1) is in development; " +
            "however, school and work have taken priority. " +
            "This game was developed to nearly match SRS (Super Rotation System), which includes wallkicks that allow " +
            "t-spin doubles, t-spin triples, and other complex movements. Scoring will also rewards these complex movements. " +
            "I hope you have as much fun playing this game as I had developing it!",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, AboutDescY),
            size: new Vector2(AboutDescWidth, AboutDescHeight),
            fontSize: AboutDescFontSize, color: new Color(1f, 0.97f, 0.95f, 0.92f), style: FontStyles.Normal,
            alignment: TextAlignmentOptions.TopLeft);
        desc.enableWordWrapping = true;

        // ── Bottom-left: "Created by" ──
        BuildCreditSection(p.transform,
            sectionAnchor: new Vector2(0f, 1f),
            header: "CREATED BY",
            name:   "CAMERON REYNES",
            alignment: TextAlignmentOptions.Left,
            links: new[]
            {
                new SocialLink { icon = SocialIcon.Email,     iconSprite = _iconEmail,
                                 label = "EMAIL",     handle = "cameronr252@gmail.com", tint = TintEmail     },
                new SocialLink { icon = SocialIcon.GitHub,    iconSprite = _iconGithub,
                                 label = "GITHUB",    handle = "@camreynes",            tint = TintGithub    },
                new SocialLink { icon = SocialIcon.Instagram, iconSprite = _iconInstagram,
                                 label = "INSTAGRAM", handle = "@camreynes",            tint = TintInstagram },
                new SocialLink { icon = SocialIcon.LinkedIn,  iconSprite = _iconLinkedin,
                                 label = "LINKEDIN",  handle = "/camreynes",            tint = TintLinkedin  },
            });

        // ── Bottom-right: "Special thanks" ──
        BuildCreditSection(p.transform,
            sectionAnchor: new Vector2(1f, 1f),
            header: "SPECIAL THANKS TO",
            name:   "GRANT HARVEY  •  MUSIC PRODUCTION",
            alignment: TextAlignmentOptions.Right,
            links: new[]
            {
                new SocialLink { icon = SocialIcon.Email,     iconSprite = _iconEmail,
                                 label = "EMAIL",     handle = "grantharvey616@gmail.com", tint = TintEmail     },
                new SocialLink { icon = SocialIcon.Instagram, iconSprite = _iconInstagram,
                                 label = "INSTAGRAM", handle = "garnt.harvey",             tint = TintInstagram },
            });

        return p;
    }

    /// <summary>
    /// Lays out one credit column inside the About panel. sectionAnchor
    /// picks which corner the column hangs off — (0,1) for the bottom-left
    /// "Created by" block (it's anchored TOP-left and grown downward via
    /// negative Y), (1,1) for the bottom-right "Special thanks" block.
    /// Header + name are drawn at the column's outer edge with the matching
    /// alignment, and each row below renders [icon] [LABEL] [handle]
    /// flowing left→right regardless of which side the column sits on, so
    /// the eye scans both columns the same way.
    /// </summary>
    private void BuildCreditSection(Transform panel, Vector2 sectionAnchor,
        string header, string name, TextAlignmentOptions alignment,
        SocialLink[] links)
    {
        bool leftCol = sectionAnchor.x < 0.5f;

        // Strip width = total horizontal footprint of one row. The 14 and 10
        // are the icon→label and label→handle gaps used in BuildCreditRow,
        // duplicated here so the header / name boxes line up with the
        // strip's outer edges. If you change the gaps in BuildCreditRow
        // update them here too — keeping these numbers as named constants
        // would be cleaner but the duplication is local enough that the
        // grep cost is fine.
        float stripW = AboutIconSize + 14f + AboutLabelWidth + 10f + AboutHandleWidth;

        // For the LEFT column the strip starts at +inset (right of anchor).
        // For the RIGHT column the strip's RIGHT edge is at -inset and it
        // extends LEFTWARD by stripW, so its left edge sits at -inset-stripW.
        // Once stripLeftX is settled the per-row layout is identical.
        float stripLeftX = leftCol
            ?  AboutSectionInsetX
            : -AboutSectionInsetX - stripW;

        // Header / name share the column's outer edge. Pivot matching the
        // anchor means anchoredPos is the corner of the text box, no
        // per-side fudge needed.
        Vector2 textPivot = sectionAnchor;
        float   textX     = leftCol ? AboutSectionInsetX : -AboutSectionInsetX;

        CreateLabel(panel, header,
            anchor: sectionAnchor, pivot: textPivot,
            anchoredPos: new Vector2(textX, AboutSectionHeaderY),
            size: new Vector2(stripW, 32f),
            fontSize: AboutHeaderFontSize, color: new Color(1f, 1f, 1f, 0.55f), style: FontStyles.Bold,
            alignment: alignment);

        CreateLabel(panel, name,
            anchor: sectionAnchor, pivot: textPivot,
            anchoredPos: new Vector2(textX, AboutSectionNameY),
            size: new Vector2(stripW, 40f),
            fontSize: AboutNameFontSize, color: new Color(1f, 0.97f, 0.95f), style: FontStyles.Bold,
            alignment: alignment);

        for (int i = 0; i < links.Length; i++)
        {
            float rowY       = AboutCreditFirstY - i * AboutCreditStep;
            float iconCenter = stripLeftX + AboutIconSize * 0.5f;
            BuildCreditRow(panel, links[i], sectionAnchor, iconCenter, rowY);
        }
    }

    /// <summary>
    /// Builds one credit row [icon] [LABEL] [handle] anchored to rowAnchor
    /// (the panel corner the column hangs off). iconCenterX is the icon's
    /// center x in that anchor's local frame; the label and handle are laid
    /// out to its right with fixed widths so the columns line up across
    /// rows. Pivot (0, 0.5) on the text means anchoredPos pins the LEFT-
    /// middle of each text box, which is the easiest thing to reason about
    /// for a left-to-right strip — even on the right side of the panel
    /// where x values are all negative, "label is to the right of icon"
    /// still means labelX > iconCenterX.
    /// </summary>
    private void BuildCreditRow(Transform panel, SocialLink link,
        Vector2 rowAnchor, float iconCenterX, float rowY)
    {
        GameObject icon = BuildSocialIcon(panel, link, AboutIconSize);
        RectTransform irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = rowAnchor;
        irt.pivot     = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(iconCenterX, rowY);

        // 14px gap from icon edge to label start. Earlier this was 10, but at
        // the new 24pt row size some PNG logos (gmail/instagram) have a hair
        // of internal padding and the label felt visually glued to them.
        float labelX  = iconCenterX + AboutIconSize * 0.5f + 14f;
        CreateLabel(panel, link.label,
            anchor: rowAnchor, pivot: new Vector2(0f, 0.5f),
            anchoredPos: new Vector2(labelX, rowY),
            size: new Vector2(AboutLabelWidth, AboutIconSize),
            fontSize: AboutRowFontSize, color: link.tint, style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left);

        // 10px gap between label box and handle box. The label box is wide
        // enough that "INSTAGRAM" (the longest label we render) finishes
        // well before the box ends, so a real visual gap shows up here even
        // though the boxes themselves are nearly touching.
        float handleX = labelX + AboutLabelWidth + 10f;
        CreateLabel(panel, link.handle,
            anchor: rowAnchor, pivot: new Vector2(0f, 0.5f),
            anchoredPos: new Vector2(handleX, rowY),
            size: new Vector2(AboutHandleWidth, AboutIconSize),
            fontSize: AboutRowFontSize, color: new Color(1f, 0.95f, 0.95f, 0.9f),
            style: FontStyles.Normal,
            alignment: TextAlignmentOptions.Left);
    }

    /// <summary>
    /// Builds the badge sitting at the start of a link row. Two rendering
    /// modes, chosen at runtime based on what's in the slot:
    ///   • Sprite mode (the common case): if `link.iconSprite` is non-null
    ///     — i.e. Resources.Load picked up the PNG from
    ///     Assets/Menu/Resources/SocialIcons/ — we render it filling the
    ///     slot with aspect preserved. We pass the iconSprite through with
    ///     a white tint instead of `link.tint`; the gmail / instagram /
    ///     etc. PNGs are full-color logos and a colored multiply would
    ///     muddy them. The `link.tint` field still drives the LABEL text.
    ///   • Procedural fallback: brand-tinted rounded square + first letter
    ///     of the label in bold white. Kicks in if a PNG fails to load
    ///     (folder moved, importer mode changed); Instagram gets a small
    ///     camera-lens motif as a nostalgic holdover from the no-emoji era.
    /// </summary>
    private GameObject BuildSocialIcon(Transform parent, SocialLink link, float size)
    {
        GameObject root = new GameObject("SocialIcon", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot     = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(size, size);

        // Loaded sprite wins. preserveAspect keeps non-square artwork from
        // getting stretched. We render at full white because the PNGs we
        // ship are full-color brand logos — multiplying them by the
        // platform's accent tint would dim and shift the colors in a way
        // that reads as "broken" rather than themed.
        if (link.iconSprite != null)
        {
            GameObject sg = new GameObject("sprite");
            sg.transform.SetParent(root.transform, false);
            Image simg = sg.AddComponent<Image>();
            simg.sprite         = link.iconSprite;
            simg.preserveAspect = true;
            simg.color          = Color.white;
            simg.raycastTarget  = false;
            RectTransform srt = sg.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            return root;
        }

        // Procedural fallback begins here. Brand-tinted rounded badge - same
        // UIRoundedSprite the rest of the UI uses, so rounding + 9-slice
        // behave identically across panels.
        GameObject bg = new GameObject("bg");
        bg.transform.SetParent(root.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.sprite = UIRoundedSprite.Default;
        bgImg.type   = Image.Type.Sliced;
        bgImg.color  = link.tint;
        bgImg.raycastTarget = false;
        RectTransform brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;

        // Instagram gets a little extra: a white square "lens" frame with an
        // inner punch-through made by layering a brand-colored square on top.
        // Every other platform just shows its first letter, which is enough
        // given the brand color does most of the recognition work.
        if (link.icon == SocialIcon.Instagram)
        {
            // Outer lens square (white frame).
            AddLockPart(root.transform, Vector2.zero,
                new Vector2(size * 0.55f, size * 0.55f), Color.white);
            // Inner punch-through (tint) so the frame reads as an outline.
            AddLockPart(root.transform, Vector2.zero,
                new Vector2(size * 0.38f, size * 0.38f), link.tint);
            // Shutter / flash dot in the upper right corner.
            AddLockPart(root.transform,
                new Vector2(size * 0.28f, size * 0.28f),
                new Vector2(size * 0.10f, size * 0.10f), Color.white);
        }
        else
        {
            // Glyph = first letter of the label. Computed at runtime so
            // renaming a slot in the Inspector doesn't require touching
            // anything else. ToUpperInvariant guards against lowercase input.
            string glyph = string.IsNullOrEmpty(link.label)
                ? "?"
                : link.label.Substring(0, 1).ToUpperInvariant();

            CreateLabel(root.transform, glyph,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(0f, 2f),          // +2 nudge to optically center bold glyphs
                size: new Vector2(size, size),
                fontSize: size * 0.62f, color: Color.white, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.Center);
        }

        return root;
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private void BuildFooter()
    {
        _hintText = CreateLabel(transform, "▲ ▼  CHANGE TAB     ENTER / SPACE  SELECT",
            anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 36f), size: new Vector2(1600f, 44f),
            fontSize: 24f, color: new Color(1f, 1f, 1f, 0.7f), style: FontStyles.Bold,
            alignment: TextAlignmentOptions.Center);
    }

    // ── Lock icon (procedural, no emoji font required) ───────────────────────

    /// <summary>
    /// Builds a procedural padlock as a child of <paramref name="parent"/>,
    /// fitting inside a <paramref name="size"/> x <paramref name="size"/> box.
    /// Composed of 4 rounded rects (wide body + U-shaped shackle made of two
    /// vertical legs and a horizontal top bar) plus a small dark keyhole dot.
    ///
    /// Replaces the 🔒 emoji, which renders as a .notdef tofu box in TMP's
    /// default LiberationSans atlas (no emoji glyphs). Returns the root so the
    /// caller can re-anchor/reposition it as needed.
    /// </summary>
    private static GameObject BuildLockIcon(Transform parent, float size, Color color)
    {
        GameObject root = new GameObject("LockIcon", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = (RectTransform)root.transform;
        rr.anchorMin = new Vector2(0.5f, 0.5f);
        rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.pivot     = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(size, size);

        // Body - the wide square that holds the keyhole.
        float bodyW = size * 0.78f;
        float bodyH = size * 0.55f;
        float bodyY = -size * 0.16f;
        AddLockPart(root.transform, new Vector2(0f, bodyY), new Vector2(bodyW, bodyH), color);

        // Shackle - two vertical legs and a horizontal top bar. Centers are
        // computed from the U's bottom (shBaseY) so the legs meet the bar.
        float shThickness = size * 0.13f;
        float shHeight    = size * 0.40f;
        float shWidth     = size * 0.56f;
        float shBaseY     = size * 0.18f;
        float shLegCenter = shBaseY + shHeight * 0.5f - shThickness * 0.5f;
        AddLockPart(root.transform,
            new Vector2(-shWidth * 0.5f + shThickness * 0.5f, shLegCenter),
            new Vector2(shThickness, shHeight), color);
        AddLockPart(root.transform,
            new Vector2( shWidth * 0.5f - shThickness * 0.5f, shLegCenter),
            new Vector2(shThickness, shHeight), color);
        AddLockPart(root.transform,
            new Vector2(0f, shBaseY + shHeight - shThickness * 0.5f),
            new Vector2(shWidth, shThickness), color);

        // Keyhole - a small dark dot on the body. Darker alpha so it reads as
        // an actual hole rather than just "another piece of padlock".
        AddLockPart(root.transform,
            new Vector2(0f, bodyY + bodyH * 0.05f),
            new Vector2(size * 0.15f, size * 0.15f),
            new Color(0f, 0f, 0f, 0.55f));

        return root;
    }

    private static void AddLockPart(Transform parent, Vector2 pos, Vector2 sz, Color color)
    {
        GameObject go = new GameObject("part");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite        = UIRoundedSprite.Default;
        img.type          = Image.Type.Sliced;
        img.color         = color;
        img.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sz;
    }

    // ── Sprite helpers ───────────────────────────────────────────────────────

    private static Sprite BuildGradientSprite()
    {
        // 1x256 vertical gradient: dark navy → deep purple.
        var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };
        Color top    = new(0.015f, 0.02f, 0.06f, 1f);
        Color bottom = new(0.12f,  0.05f, 0.22f, 1f);
        Color32[] px = new Color32[256];
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            px[i] = Color.Lerp(bottom, top, t);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite _gridSpriteCached;
    private static Sprite BuildGridSprite()
    {
        if (_gridSpriteCached != null) return _gridSpriteCached;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Repeat,
            hideFlags  = HideFlags.HideAndDontSave
        };
        Color32 off = new(0, 0, 0, 0);
        Color32 on  = new(255, 255, 255, 255);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = (x == 0 || y == 0) ? on : off;
        tex.SetPixels32(px);
        tex.Apply(false, true);
        _gridSpriteCached = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _gridSpriteCached;
    }

    // ── Misc helpers ──────────────────────────────────────────────────────────────────────

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// TextMeshPro label helper - same shape as GameOverScreen's but with
    /// configurable anchor/pivot so we can pin to corners instead of center.
    /// </summary>
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
