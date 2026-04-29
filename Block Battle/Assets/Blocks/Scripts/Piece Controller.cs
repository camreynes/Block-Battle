using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Class that handles piece spawning, rotations, movements, etc.
public class PieceController : MonoBehaviour
{

    [SerializeField] private GameObject[] _tetrominoPrefab;
    [SerializeField] protected BlockGrid _grid;

    private List<Tuple<int, GameObject>> _pieceOrder    = new List<Tuple<int, GameObject>>();
    private List<Tuple<int, GameObject>> _tempPieceList = new List<Tuple<int, GameObject>>();

    private Vector2Int[] _lastPositions;

    // Fall speed derived from level using the official Guideline formula each spawn.
    // Level 1 → 1.0 s/row  |  Level 10 → ~0.064 s/row  |  Level 15 → ~0.007 s/row (cap)
    private float _timeToFall   = 1.0f;

    // Lock-phase timers — recomputed each spawn via GetLock*ForLevel().
    // All three shrink with level so high-level play feels appropriately aggressive.
    private float _lockDelay    = 0.5f;   // per-step budget before the piece locks
    private float _maxLockDelay = 1.5f;   // absolute ceiling for the whole lock phase
    private int   _maxMoveResets = 15;    // how many move/rotate resets the player gets

    private PieceScript _currentPiece;
    private int _playerID  = -1;  // Player ID for input mapping
    private int _spawnHeld = -1;  // ≥0 means spawn this held piece type next

    private HoldState _holdLeft;
    private HoldState _holdRight;
    private HoldState _holdDown;

    private bool _recentlyMovedByPlayer   = false;
    private bool _recentlyRotatedByPlayer = false;
    private bool _lastMoveRotate          = false; // false = move, true = rotate (for t-spin detection)
    private bool _forceHardDrop           = false;
    private bool _recentlyHeld            = false; // prevents double-hold per piece
    private bool _gameOver                = false; // latched on top-out

    [SerializeField] private bool _setPiece    = false; // debug: always spawn piece 4
    [SerializeField] private bool _stagePreset = false;

    // Soft-drop row counter — reset on every new piece, flushed to score on lock
    private int _softDropRows = 0;

    private Preview _preview;
    private Outline _outline;
    private Hold    _hold;

    private static readonly Vector2Int LEFT  = new Vector2Int(-1, 0);
    private static readonly Vector2Int RIGHT = new Vector2Int(1, 0);
    private static readonly Vector2Int DOWN  = new Vector2Int(0, -1);

    private Coroutine _fallRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _holdLeft  = new HoldState(0.167f, 0.033f);
        _holdRight = new HoldState(0.167f, 0.033f);
        _holdDown  = new HoldState(0.05f,  0.02f);
    }

    private void Start()
    {
        if (_stagePreset)
        {
            Global.GetPreset();
            GameObject pieceObj = Instantiate(_tetrominoPrefab[1]);
            _currentPiece = pieceObj.GetComponent<PieceScript>();
            _currentPiece.SetGrid(_grid);

            Vector2Int[] initialPositions = Global.scenePreset.ToArray();
            _currentPiece.SetPositions(initialPositions);
            _currentPiece.SpawnBlocks(initialPositions);

            _currentPiece.SetBlocksInactive(gameObject);
            PieceInfo pieceInfo = new PieceInfo(_currentPiece.GetPieceType(), _lastMoveRotate, _lastPositions[0]);
            _currentPiece.FinishDestroy(pieceInfo);
            _currentPiece = null;
        }
        SpawnPiece();
        _fallRoutine = StartCoroutine(BlockFall());
    }

    // ── Input / Update ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (_gameOver) return;

        // ── Pause gate ───────────────────────────────────────────────────────────
        // Time.timeScale = 0 (set by PauseMenu) freezes coroutines because their
        // timers use Time.deltaTime, but MonoBehaviour.Update() runs unaffected.
        // Without this gate, the player can still move/rotate/hard-drop the piece
        // through the pause overlay.
        //
        // We also stop any DAS hold state so a direction that was held when the
        // player paused doesn't make the piece drift the moment they resume.
        // After unpause the player has to re-press; that mirrors how every
        // mainline Tetris does it and avoids surprise movement on resume.
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)
        {
            _holdLeft.StopHold();
            _holdRight.StopHold();
            _holdDown.StopHold();
            return;
        }
        // ─────────────────────────────────────────────────────────────────────────

        // ── Key-release events must be processed even when no piece is active ──
        // BUG FIX: previously the early-return on _currentPiece == null would swallow
        // WasReleasedThisFrame() calls, leaving HoldState stuck in IsHolding=true.
        // The new piece would then immediately move in the held direction on spawn.
        var actLeft  = TetrixInputManager.GetInputAction(GameInputAction.MOVE_LEFT,  _playerID);
        var actRight = TetrixInputManager.GetInputAction(GameInputAction.MOVE_RIGHT, _playerID);
        var actDown  = TetrixInputManager.GetInputAction(GameInputAction.SOFT_DROP,  _playerID);

        if (actLeft  != null && actLeft.WasReleasedThisFrame())  OnMoveEnd(LEFT);
        if (actRight != null && actRight.WasReleasedThisFrame()) OnMoveEnd(RIGHT);
        if (actDown  != null && actDown.WasReleasedThisFrame())  OnMoveEnd(DOWN);
        // ─────────────────────────────────────────────────────────────────────────

        // ── Capture new presses during spawn gap ─────────────────────────────────
        // WasPressed fires for exactly one frame. If that frame falls inside the
        // spawn gap (_currentPiece == null), the regular WasPressed block below
        // is never reached and the input is dropped. Priming IsHolding here means
        // DAS/ARR picks it up the moment the new piece arrives.
        if (_currentPiece == null)
        {
            if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT,  _playerID)) _holdLeft.StartHold();
            if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, _playerID)) _holdRight.StartHold();
            if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP,  _playerID)) _holdDown.StartHold();
            return;
        }

        // ── Outline update (optimized: only allocate when player actually moved) ──
        // GetPositions() + comparison are skipped on idle frames to avoid GC pressure.
        if (_recentlyMovedByPlayer || _recentlyRotatedByPlayer)
        {
            var currentPositions = _currentPiece.GetPositions();
            if (_lastPositions == null || !PositionsEqual(_lastPositions, currentPositions))
                _outline.UpdateOutline(_currentPiece.GetOutlineVectors(), _currentPiece.GetPieceType());
            _lastPositions = currentPositions;
        }

        // ── Reset per-frame movement flags ───────────────────────────────────────
        _recentlyMovedByPlayer   = false;
        _recentlyRotatedByPlayer = false;

        bool leftHeld  = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld  = _holdDown.IsHolding;

        // ── DAS / ARR movement ───────────────────────────────────────────────────
        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
        {
            bool moved = _currentPiece.TryMovePiece(LEFT);
            _recentlyMovedByPlayer = moved;
            if (moved) GameplaySFXManager.Instance?.PlayMove();
        }
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
        {
            bool moved = _currentPiece.TryMovePiece(RIGHT);
            _recentlyMovedByPlayer = moved;
            if (moved) GameplaySFXManager.Instance?.PlayMove();
        }
        if (downHeld && _holdDown.ShouldRepeat())
        {
            bool moved = _currentPiece.TryMovePiece(DOWN);
            _recentlyMovedByPlayer = moved;
            if (moved) _softDropRows++;
        }

        // ── New key presses ───────────────────────────────────────────────────────
        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT,  _playerID)) OnMoveStart(LEFT);
        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, _playerID)) OnMoveStart(RIGHT);
        if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP,  _playerID)) OnMoveStart(DOWN);

        if (TetrixInputManager.WasPressed(GameInputAction.HARD_DROP, _playerID)) HardDrop();

        // ── Rotations ─────────────────────────────────────────────────────────────
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CW, _playerID))
        {
            _recentlyRotatedByPlayer = _currentPiece.TryRotateCW();
            if (_recentlyRotatedByPlayer) GameplaySFXManager.Instance?.PlayRotate();
        }
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CCW, _playerID))
        {
            _recentlyRotatedByPlayer = _currentPiece.TryRotateCCW();
            if (_recentlyRotatedByPlayer) GameplaySFXManager.Instance?.PlayRotate();
        }

        // ── Hold ──────────────────────────────────────────────────────────────────
        if (TetrixInputManager.WasPressed(GameInputAction.HOLD, _playerID))
            HoldPiece();

        // ── Debug ─────────────────────────────────────────────────────────────────
        if (TetrixInputManager.WasPressed(GameInputAction.SAVE_SCENE, _playerID))
            SaveScene();

        if (_recentlyMovedByPlayer)   _lastMoveRotate = false;
        if (_recentlyRotatedByPlayer) _lastMoveRotate = true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Official Tetris Guideline fall-speed formula (matches play.tetris.com).
    ///   G = (0.8 − 0.007·(L−1))^(L−1) seconds per row
    /// Level 1 → 1.000 s | Level 5 → 0.355 s | Level 10 → 0.064 s | Level 15 → 0.007 s (cap)
    /// </summary>
    private float GetFallSpeedForLevel(int level)
    {
        float b = 0.8f - (level - 1) * 0.007f;
        return Mathf.Max(Mathf.Pow(b, level - 1), 0.001f); // floor at 1 ms so it never hits 0
    }

    /// <summary>
    /// Per-step lock budget. 500 ms at level 1, linear ramp to 100 ms at level 21+.
    /// Mirrors the aggressive feel of competitive games (TETR.IO / Jstris).
    /// </summary>
    private float GetLockDelayForLevel(int level)
    {
        return Mathf.Max(0.10f, 0.50f - (level - 1) * 0.02f);
        // Level 1 → 500 ms | Level 10 → 320 ms | Level 15 → 220 ms | Level 21+ → 100 ms
    }

    /// <summary>
    /// How many move/rotate resets the player gets before the step timer becomes
    /// irresistible. Standard SRS allows 15; scales down at higher levels so
    /// infinite floor-sliding is impossible in fast play.
    /// </summary>
    private int GetMaxResetsForLevel(int level)
    {
        return Mathf.Max(5, 15 - Mathf.Max(0, level - 5));
        // Levels 1–5 → 15 | Level 10 → 10 | Level 15+ → 5
    }

    /// <summary>
    /// Zero-allocation position comparison (replaces LINQ SequenceEqual).
    /// </summary>
    private static bool PositionsEqual(Vector2Int[] a, Vector2Int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    // ── Piece Order ───────────────────────────────────────────────────────────────

    private void CreateNewOrder()
    {
        _tempPieceList.Clear();
        for (int i = 0; i < _tetrominoPrefab.Length; i++)
            _tempPieceList.Add(new Tuple<int, GameObject>(i, _tetrominoPrefab[i]));

        for (int i = 0; i < _tetrominoPrefab.Length; i++)
        {
            int idx = UnityEngine.Random.Range(0, _tempPieceList.Count);
            _pieceOrder.Add(_tempPieceList[idx]);
            _tempPieceList.RemoveAt(idx);
        }
    }

    // ── Move Helpers ──────────────────────────────────────────────────────────────

    private void HardDrop()
    {
        if (_currentPiece == null) return;

        // Measure drop distance for the 2 pt/row bonus (no level multiplier per guideline)
        Vector2Int[] before = _currentPiece.GetPositions();
        int startY = before[0].y;
        for (int i = 1; i < before.Length; i++)
            if (before[i].y < startY) startY = before[i].y;

        _forceHardDrop = true;
        _currentPiece.HardDrop();

        Vector2Int[] after = _currentPiece.GetPositions();
        int endY = after[0].y;
        for (int i = 1; i < after.Length; i++)
            if (after[i].y < endY) endY = after[i].y;

        int rowsDropped = startY - endY;
        if (rowsDropped > 0) _grid.AddDropPoints(rowsDropped * 2);

        GameplaySFXManager.Instance?.PlayHardDrop();
        // NOTE: piece locking is handled by BlockFall/LockDelay via _forceHardDrop
    }

    private void HoldPiece()
    {
        if (_recentlyHeld || _currentPiece == null) return;
        _recentlyHeld = true;
        GameplaySFXManager.Instance?.PlayHold();
        _spawnHeld = _hold.UpdateHold((int)_currentPiece.GetPieceType());
        Destroy(_currentPiece.gameObject);
        _currentPiece = null;
    }

    private void OnMoveStart(Vector2Int direction)
    {
        if (direction.x < 0)
            _holdLeft.StartHold();
        else if (direction.x > 0)
            _holdRight.StartHold();
        else if (direction.y < 0)
        {
            _holdDown.StartHold();
            GameplaySFXManager.Instance?.PlaySoftDrop();
        }
        _recentlyMovedByPlayer = true;
    }

    private void OnMoveEnd(Vector2Int direction)
    {
        if (direction.x < 0)
            _holdLeft.StopHold();
        else if (direction.x > 0)
            _holdRight.StopHold();
        else if (direction.y < 0)
            _holdDown.StopHold();
    }

    // ── Spawn Piece ───────────────────────────────────────────────────────────────

    private void SpawnPiece()
    {
        int level       = _grid.GetLevel();
        _timeToFall     = GetFallSpeedForLevel(level);
        _lockDelay      = GetLockDelayForLevel(level);
        _maxLockDelay   = _lockDelay * 3f;   // absolute ceiling = 3× the step budget
        _maxMoveResets  = GetMaxResetsForLevel(level);
        _softDropRows   = 0;
        _forceHardDrop  = false;
        GameObject pieceObj = null;

        if (_pieceOrder.Count <= 5) CreateNewOrder();

        if (_spawnHeld >= 0)
        {
            pieceObj   = Instantiate(_tetrominoPrefab[_spawnHeld]);
            _spawnHeld = -1;
        }
        else if (_setPiece)
            pieceObj = Instantiate(_tetrominoPrefab[4]);
        else
        {
            pieceObj = Instantiate(_pieceOrder[0].Item2);
            _pieceOrder.RemoveAt(0);
        }

        int[] preview = { _pieceOrder[0].Item1, _pieceOrder[1].Item1,
                          _pieceOrder[2].Item1, _pieceOrder[3].Item1 };
        _preview.UpdatePreview(preview);

        _currentPiece = pieceObj.GetComponent<PieceScript>();
        _currentPiece.SetGrid(_grid);
        Vector2Int[] initialPositions = _currentPiece.GetInitialPositions();
        _currentPiece.SetPositions(initialPositions);

        if (!_currentPiece.CheckBlockLocations(initialPositions))
        {
            // Top-out — trigger game over
            _gameOver = true;
            if (_fallRoutine != null) StopCoroutine(_fallRoutine);
            Destroy(_currentPiece.gameObject);
            _currentPiece = null;

            // Fold this run's records into the lifetime PlayerStats. Has to
            // happen BEFORE ShowGameOver — the game-over screen is what the
            // player will press a key on to restart, and a stale "current run"
            // would otherwise carry over.
            PlayerStats.EndRun(_grid.GetTotalScore(), _grid.GetLevel(), _grid.GetPeakStreak());

            GameOverScreen.ShowGameOver(_grid.GetTotalScore(), _grid.GetLevel(), _grid.GetLinesCleared());
            return;
        }

        _currentPiece.SpawnBlocks(initialPositions);
        _lastPositions = _currentPiece.GetPositions(); // copy (not alias) so PositionsEqual stays valid as the piece moves
        // NOTE: _recentlyHeld is intentionally NOT reset here.
        // It is only cleared in SetBlocksInactive (natural lock) so that a piece
        // that arrived via a hold swap cannot be immediately swapped again.
        // Resetting here was the bug that allowed infinite hold cycling.
        _outline.UpdateOutline(_currentPiece.GetOutlineVectors(), _currentPiece.GetPieceType());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────────

    private IEnumerator BlockFall()
    {
        while (true)
        {
            if (_currentPiece == null)
            {
                yield return new WaitForSeconds(0.05f);
                SpawnPiece();
                continue;
            }

            if (_currentPiece.TestOffset(DOWN))
                yield return WaitAndFall();
            else
                yield return LockDelay();
        }
    }

    private IEnumerator WaitAndFall()
    {
        float timer = 0f;
        while (timer < _timeToFall && !_forceHardDrop && _currentPiece != null)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (!_forceHardDrop && _currentPiece != null)
            _currentPiece.TryMovePiece(DOWN);
    }

    private IEnumerator LockDelay()
    {
        // ── SRS Extended Placement / Lock Delay ───────────────────────────────────
        // Rules (Tetris Guideline + competitive scaling):
        //   • Player gets _lockDelay seconds per grounded step (shrinks with level).
        //   • Each successful move/rotate resets the step timer, up to _maxMoveResets
        //     times total (also shrinks with level — prevents infinite floor-sliding).
        //   • _maxLockDelay is an absolute ceiling on the whole lock phase (3× step).
        //   • If a move/rotate kicks the piece off the floor, lock phase exits cleanly
        //     and gravity resumes at the normal _timeToFall rate (no immediate drop).
        // ─────────────────────────────────────────────────────────────────────────

        float stepTimer  = 0f;
        float totalTimer = 0f;
        int   moveResets = 0;

        while (!_forceHardDrop && _currentPiece != null)
        {
            float dt = Time.deltaTime;
            stepTimer  += dt;
            totalTimer += dt;

            if (_recentlyMovedByPlayer || _recentlyRotatedByPlayer)
            {
                // Piece moved/rotated off the floor — hand control back to gravity
                // WITHOUT an immediate drop so the full _timeToFall delay applies.
                if (_currentPiece.TestOffset(DOWN))
                    yield break;

                // Still grounded — reset the step timer if resets remain
                if (moveResets < _maxMoveResets)
                {
                    stepTimer = 0f;
                    moveResets++;
                }
            }

            if (stepTimer >= _lockDelay || totalTimer >= _maxLockDelay)
                break;

            yield return null;
        }

        // Lock the piece (natural expiry or hard drop)
        if (_currentPiece != null)
            yield return SetBlocksInactive();
    }

    private IEnumerator SetBlocksInactive()
    {
        // Snapshot the piece into a local reference and immediately null out
        // the field BEFORE we yield anywhere. This closes a race condition
        // that could orphan an entire row of blocks:
        //
        // The "isFull" path below yields WaitForSeconds(Global.effectDuration)
        // — about 110 ms of shine animation — between marking the blocks
        // inactive and actually calling ClearRows. During that wait Update()
        // keeps running, and back when this method held _currentPiece across
        // the yield two things could happen:
        //
        //   1. HoldPiece() saw a non-null _currentPiece, swapped the just-
        //      locked piece into the hold slot, Destroy()'d the piece, and
        //      set _currentPiece = null. When the coroutine resumed, the
        //      _currentPiece.GetPieceType() call NRE'd, FinishDestroy never
        //      ran, ClearRows never ran, and the full rows stayed on the
        //      board for the rest of the run.
        //
        //   2. A rotate input during the shine window kicked the locked
        //      piece into nearby empty cells. NullGridLocations() then
        //      cleared the just-locked references out of _blocksInGrid,
        //      and even though the visible blocks were still parented to
        //      the row, CheckRowsFull stopped seeing the row as full.
        //
        // Both cases reproduce the bug Cam reported (a placement that
        // visually settles but doesn't trigger a clear). Detaching the
        // field up front means Update()'s _currentPiece == null guard
        // turns every "during shine" input into a no-op, which is what
        // the visible behaviour already implies — the piece is committed.
        PieceScript piece = _currentPiece;
        if (piece == null) yield break;
        _currentPiece = null;

        // Award soft-drop bonus (1 pt/row, no level multiplier per guideline)
        if (_softDropRows > 0) _grid.AddDropPoints(_softDropRows);

        // Capture rotation + pivot BEFORE SetBlocksInactive runs. We use the live
        // piece's current pivot (positions[0]) instead of _lastPositions[0] because
        // _lastPositions only updates on player input — gravity drops and hard drops
        // would otherwise leave it stale, which silently breaks T-spin corner checks.
        int rotationAtLock = piece.GetRotation();
        Vector2Int[] livePositions = piece.GetPositions();
        Vector2Int  pivotAtLock    = livePositions.Length > 0 ? livePositions[0] : Vector2Int.zero;

        bool isFull = piece.SetBlocksInactive(gameObject);
        if (isFull)
        {
            yield return new WaitForSeconds(Global.effectDuration);
            PieceInfo pieceInfo = new PieceInfo(
                piece.GetPieceType(), _lastMoveRotate, pivotAtLock, rotationAtLock);
            _grid.IncrementScore();
            piece.FinishDestroy(pieceInfo);
        }
        else
        {
            // No lines cleared, but a 0-line T-spin / Mini T-spin still scores per
            // Tetris guideline (T-Spin = 400 × level, Mini T-Spin = 100 × level)
            // and maintains the B2B chain. Combo, however, only counts line clears,
            // so we still reset the streak below.
            PieceInfo pieceInfo = new PieceInfo(
                piece.GetPieceType(), _lastMoveRotate, pivotAtLock, rotationAtLock);
            _grid.OnPieceLockNoClear(pieceInfo);
            _grid.ResetScoreStreak();
        }

        _recentlyHeld = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    public void SetGrid(BlockGrid grid)     { _grid    = grid;    }
    public void SetHold(Hold hold)          { _hold    = hold;    }
    public void SetPreview(Preview preview) { _preview = preview; }
    public void SetOutline(Outline outline) { _outline = outline; }
    public void SetPlayerID(int id)         { _playerID = id;     }

    public static void PrintVector2Array(Vector2Int[] vectors, string label = "Vector2 Array")
    {
        string result = label + ": [ ";
        foreach (Vector2 v in vectors) result += $"({v.x}, {v.y}) ";
        result += "]";
        Debug.Log(result);
    }

    public void SaveScene()
    {
        Debug.Log($"Scene Saved with {Global.scenePreset.Count} Blocks");
        SaveUtility.Save(Global.scenePreset);
    }
}
