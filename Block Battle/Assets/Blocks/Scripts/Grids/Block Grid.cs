using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class BlockGrid : MonoBehaviour
{
    //public (float, float)[,] positions = new (float, float)[20, 10]; // 20x10 grid filled with tuples representing the bottom left positions of each space
    private double _width = 0.449481296539307;
    private double _height = 0.447401666641235;
    private int _rows = 20;
    private int _cols = 10;
    private int _playerID;
    private int _maxHeight = 20;
    private int _spaceUsed = 0; // Tracks the top most block being used
    private int[] _blockCount = new int[20]; // Tracks the number of blocks in each row
    private int _scoreStreak = 0;
    private int _peakStreak  = 0;          // highest streak seen this run — folded into PlayerStats at game-over
    private int _totalScore = 0;
    private int _linesCleared = 0;
    private int _level = 1;
    private bool _backToBack = false;      // true when last eligible clear was Tetris or T-Spin

    // Per-line-count All Clear bonuses (multiplied by level). Matches the
    // official Tetris Guideline / play.tetris.com:
    //   1-line PC = 800 × level
    //   2-line PC = 1200 × level
    //   3-line PC = 1800 × level
    //   4-line PC = 2000 × level
    // A B2B chain that ends in a 4-line PC adds +1200 × level on top
    // (the "B2B All Clear" bonus). Indexed by (linesCleared - 1).
    private static readonly int[] PerfectClearBonusByLines = { 800, 1200, 1800, 2000 };
    private const int B2BPerfectClearBonus = 1200;
    private ScoreTracker _scoreTracker;

    private Vector2 _position; //represents bottom left position of the grid
    public Vector2 scale = new Vector2(0, 0);

    private GameObject[][] _blocksInGrid = new GameObject[20][]; // Initialize array of gameObjects (cubes) representning the filled array
    private GameObject[] _parentRows = new GameObject[20]; // Tracks the number of blocks in each row


    //private void Awake()
    //{
    //    // Initialize the inner array to avoid null reference issues
    //    InitializeGrid();
    //}

    public void InitializeSelf(int playerID)
    {
        // Fill jagged array
        for (int i = 0; i < _rows; i++)
        {
            _blocksInGrid[i] = new GameObject[_cols];
        }

        _position = new Vector2(transform.position.x, transform.position.y);
        scale = gameObject.transform.localScale;
        _playerID = playerID;
    }

    public void InitializeDimens()
    {
        // Get the bounds of the gameObject and find height and width
        SpriteRenderer gridRenderer = gameObject.GetComponent<SpriteRenderer>();
        Bounds gridBounds = gridRenderer.bounds;

        double width = gridBounds.size.x / _cols;
        double height = gridBounds.size.y / _rows;

        // Initialize private widh and height (We do this in case we use a custom grid rather than the default 20x10 one)
        _width = width;
        _height = height;
    }

    // -----------------------GETTERS AND SETTERS-----------------------

    // Public Methods
    // Returns (bottom left) position in grid based on the row and column
    // Rows count up from 0 to 19, Columns left to right from 0 to 9
    public Vector3 GetPosInGrid(int x, int y)
    {
        //Debug.Log($"{_position}");
        return new Vector3((float)(_position.x + _width * x), (float)(_position.y + _height * y), 0);
    }
    public int GetPlayerID()
    {
        return _playerID;
    }

    public GameObject GetParentRow(int y)
    {
        if (_parentRows[y] == null)
            _parentRows[y] = new GameObject($"Row_{y}");
        return _parentRows[y];
    }

    // More Public Methods, used to be in block class
    // Method to change the grid array
    public void SetBlockInGridArray(GameObject block, int x, int y)
    {
        _blocksInGrid[y][x] = block;
        if (y >= _spaceUsed)
            _spaceUsed = y;
    }

    public void IncrementBlockCount(int y)
    {
        _blockCount[y]++;
    }

    public double GetTotalWidth()
    {
        return _width * _cols;
    }

    public double GetTotalHeight()
    {
        return _height * _rows;
    }

    // -----------------------CHECKING METHODS-----------------------


    // Method to check if space is avaiable based on offset, long method for debug atm
    public bool CheckSpace(int x, int y)
    {
        // Check if coordinates are out of bounds 
        if (x < 0 || x > 9 || y < 0 || y > _maxHeight - 1)
            return false;

        // Return true if the space is empty or the block is active
        bool isValid = _blocksInGrid[y][x] == null || _blocksInGrid[y][x].GetComponent<Block>().GetBlockStatus();
        return isValid;
    }

    /// <summary>
    /// Checks changed rows and returns full ones
    /// </summary>
    /// <param name="changedHeights"></param>
    /// <returns></returns>
    public List<int> CheckRowsFull(List<int> changedHeights)
    {
        // Filter Changed Heights // CHECKING
        changedHeights.Sort();
        for (int i = changedHeights.Count - 1; i >= 0; i--)
        {
            if (!(_blockCount[changedHeights[i]] == _cols))
            {
                changedHeights.RemoveAt(i);
            }
        }
        return changedHeights;
    }

    /// <summary>
    /// Shine effect for full rows, changed vertical dissolve float within the dissolve shader map
    /// </summary>
    /// <param name="fullRows">List of sorted rows that are to be cleared</param>
    public void ShineEffect(List<int> fullRows)
    {
        for (int i = 0; i < fullRows.Count; i++)
        {
            int y = fullRows[i];
            for (int c = 0; c < _cols; c++)
            {
                GameObject block = _blocksInGrid[y][c];
                if (block == null) // Shouldn't be nulll, just in case
                    continue;

                // Two different effects depending on whether the cleared row is surrounded by other full rows or not
                if (fullRows.Contains(y - 1)  ||  fullRows.Contains(y + 1))
                    block.GetComponent<Block>().GetComponent<Dissolve>().NormalDissolve();
                else
                    block.GetComponent<Block>().GetComponent<Dissolve>().VerticalDissolve();
            }
        }
    }

    public void SetScoreTracker(ScoreTracker scoreTracker)
    {
        _scoreTracker = scoreTracker;
    }

    public void ResetScoreStreak()
    {
        _scoreStreak = 0;
        _scoreTracker?.ClearComboDisplay(_level, _linesCleared);
    }

    public void IncrementScore()
    {
        _scoreStreak++;
        if (_scoreStreak > _peakStreak) _peakStreak = _scoreStreak;
    }

    public int GetTotalScore()   => _totalScore;
    public int GetLevel()        => _level;
    public int GetLinesCleared() => _linesCleared;
    /// <summary>Peak combo streak during this run — used by GameOverScreen / PlayerStats.</summary>
    public int GetPeakStreak()   => _peakStreak;

    /// <summary>
    /// Adds soft-drop (1 pt/row) or hard-drop (2 pt/row) bonus to the total score.
    /// Called directly from PieceController so the display stays in sync.
    /// </summary>
    public void AddDropPoints(int points)
    {
        if (points <= 0) return;
        _totalScore += points;
        _scoreTracker?.UpdateScore(_totalScore, "", _scoreStreak, _level, _linesCleared);
    }

    // Official Tetris Guideline scoring (× Level) — matches play.tetris.com
    // Single 100 | Double 300 | Triple 500 | Tetris 800
    // T-Spin (0 lines) 400 | T-Spin Single 800 | T-Spin Double 1200 | T-Spin Triple 1600
    // Mini T-Spin (0 lines) 100 | Mini T-Spin Single 200
    // B2B bonus: ×1.5 on the base for consecutive difficult clears (Tetris, T-spin, Mini)
    // Combo: 50 × (streak - 1) × level (0 on first consecutive clear)
    // Soft drop: 1 pt/row  |  Hard drop: 2 pts/row  (no level multiplier – handled in PieceController)
    // Perfect Clear: per-line-count bonus (800/1200/1800/2000 × level), +1200 × level if B2B 4-line
    private void CalculcateScore(List<Tuple<int, int, int>> rowsToShift, PieceInfo info)
    {
        int totalRowsCleared = 0;
        for (int i = 0; i < rowsToShift.Count; i++)
            totalRowsCleared += rowsToShift[i].Item3;

        if (totalRowsCleared == 0)
            return;

        // --- T-spin detection (full vs mini) ---
        // Full T-spin: T piece + last move was rotation + 3+ total corners + 2+ FRONT corners
        // Mini T-spin: same conditions but only 0–1 front corners (so 2+ back corners)
        // "Front" corners are the two corners adjacent to the side the T's stub points to.
        bool tspinAny = info.pieceType == PieceType.T
                        && info.lastMoveRotate
                        && GetCornersOccupied(info.centerPos) >= 3;
        bool tspinFull = false;
        bool tspinMini = false;
        if (tspinAny)
        {
            int frontCorners = GetFrontCornersOccupied(info.centerPos, info.rotationState);
            if (frontCorners >= 2) tspinFull = true;
            else                   tspinMini = true;
        }

        int basePoints = 0;
        string clearType = "";
        bool b2bEligible = false; // Tetris, T-spin (full or mini) qualify for back-to-back

        switch (totalRowsCleared)
        {
            case 1:
                if (tspinFull)
                {
                    clearType = "T-SPIN SINGLE";
                    basePoints = 800;
                    b2bEligible = true;
                }
                else if (tspinMini)
                {
                    clearType = "MINI T-SPIN SINGLE";
                    basePoints = 200;
                    b2bEligible = true;
                }
                else
                {
                    clearType = "SINGLE";
                    basePoints = 100;
                }
                break;

            case 2:
                if (tspinFull)
                {
                    clearType = "T-SPIN DOUBLE";
                    basePoints = 1200;
                    b2bEligible = true;
                }
                else
                {
                    clearType = "DOUBLE";
                    basePoints = 300;
                }
                break;

            case 3:
                if (tspinFull)
                {
                    clearType = "T-SPIN TRIPLE";
                    basePoints = 1600;
                    b2bEligible = true;
                }
                else
                {
                    clearType = "TRIPLE";
                    basePoints = 500;
                }
                break;

            case 4:
                clearType = "TETRIS";
                basePoints = 800;
                b2bEligible = true;
                break;

            default:
                break;
        }

        // --- Back-to-back bonus: ×1.5 on base for consecutive difficult clears ---
        // Capture the chain state BEFORE updating it so we can apply the B2B
        // Perfect Clear bonus correctly below (which depends on whether we
        // were already in a B2B chain coming into this Tetris).
        bool wasB2B = _backToBack;
        if (b2bEligible && wasB2B)
        {
            basePoints = Mathf.RoundToInt(basePoints * 1.5f);
            clearType = "B2B " + clearType;
        }
        _backToBack = b2bEligible; // any non-eligible clear (Single/Double/Triple) breaks the chain

        // --- Official level multiplier on base points ---
        int linesClearPoints = basePoints * _level;

        // --- Combo bonus: 50 × (streak - 1) × level (0 on the very first consecutive clear) ---
        int comboBonus = (_scoreStreak > 1) ? 50 * (_scoreStreak - 1) * _level : 0;

        // --- Perfect clear: per-line-count bonus, +1200 × level if B2B 4-line ---
        // Detection happens AFTER ClearRows has already wiped the rows but
        // BEFORE the post-clear shifts run, so an empty grid here means the
        // clear emptied the board.
        bool perfect = IsBoardEmptyAfterClear();
        int perfectBonus = 0;
        if (perfect && totalRowsCleared >= 1 && totalRowsCleared <= 4)
        {
            perfectBonus = PerfectClearBonusByLines[totalRowsCleared - 1] * _level;
            // B2B All Clear bonus: only applies to a 4-line PC that's part of an
            // existing B2B chain (i.e., wasB2B == true and this is a Tetris).
            if (wasB2B && totalRowsCleared == 4)
                perfectBonus += B2BPerfectClearBonus * _level;
            clearType = "PERFECT " + clearType;
        }

        int earned = linesClearPoints + comboBonus + perfectBonus;
        _totalScore += earned;

        Debug.Log($"[Lvl {_level}] {clearType} | +{earned} (base:{linesClearPoints} combo:{comboBonus} perfect:{perfectBonus}) | Total:{_totalScore} | Streak:{_scoreStreak}");

        // --- Lifetime stats hook ---
        // Strip the PERFECT marker before recording so RecordClear's switch
        // matches the canonical clear-type names. The perfect counter is its
        // own dedicated bump.
        string statClearType = perfect ? clearType.Substring("PERFECT ".Length) : clearType;
        PlayerStats.RecordClear(statClearType);
        if (perfect) PlayerStats.RecordPerfectClear();

        _scoreTracker?.UpdateScore(_totalScore, clearType, _scoreStreak, _level, _linesCleared);
        GameplaySFXManager.Instance?.PlayClearType(clearType);
    }

    /// <summary>
    /// Called by PieceController when a piece locks WITHOUT clearing any lines.
    /// 0-line T-spins still score (T-Spin = 400 × level, Mini T-Spin = 100 × level)
    /// and maintain the B2B chain even though no lines were cleared. Combo is
    /// handled separately by the caller — line clears are the only thing that
    /// extend a combo, so this method does not touch _scoreStreak.
    /// </summary>
    public void OnPieceLockNoClear(PieceInfo info)
    {
        if (info.pieceType != PieceType.T || !info.lastMoveRotate) return;
        if (GetCornersOccupied(info.centerPos) < 3) return;

        bool isMini = GetFrontCornersOccupied(info.centerPos, info.rotationState) < 2;
        int basePoints = isMini ? 100 : 400;
        string clearType = isMini ? "MINI T-SPIN" : "T-SPIN";

        // 0-line T-spins ARE B2B-eligible per Tetris Guideline. Apply ×1.5 if
        // already in chain, then maintain/start the chain regardless.
        if (_backToBack)
        {
            basePoints = Mathf.RoundToInt(basePoints * 1.5f);
            clearType = "B2B " + clearType;
        }
        _backToBack = true;

        int earned = basePoints * _level;
        _totalScore += earned;

        Debug.Log($"[Lvl {_level}] {clearType} (no lines) | +{earned} | Total:{_totalScore}");

        string statClearType = clearType.StartsWith("B2B ") ? clearType.Substring(4) : clearType;
        PlayerStats.RecordClear(statClearType);

        _scoreTracker?.UpdateScore(_totalScore, clearType, _scoreStreak, _level, _linesCleared);
        GameplaySFXManager.Instance?.PlayClearType(clearType);
    }

    /// <summary>
    /// True iff every row's block count is zero. Called right after ClearRows
    /// has wiped the cleared rows (and reset their _blockCount entries to 0)
    /// but before ShiftRows runs — at that moment, all-zero counts mean
    /// nothing was left stacked above the cleared rows either, which is the
    /// perfect-clear condition.
    ///
    /// Using _blockCount instead of scanning _blocksInGrid avoids a subtle
    /// timing trap: Destroy() is queued to end-of-frame, so the GameObject
    /// references in _blocksInGrid may still be valid (or not) when we ask.
    /// _blockCount, on the other hand, is updated synchronously by ClearRows,
    /// so this check is reliable.
    /// </summary>
    private bool IsBoardEmptyAfterClear()
    {
        for (int y = 0; y < _maxHeight; y++)
            if (_blockCount[y] != 0) return false;
        return true;
    }

    // helper method for detecting t-spins
    private int GetCornersOccupied(Vector2Int centerPos)
    {
        int occupiedCorners = 0;
        occupiedCorners += IsOccupied(centerPos.x+1, centerPos.y+1);
        occupiedCorners += IsOccupied(centerPos.x-1, centerPos.y+1);
        occupiedCorners += IsOccupied(centerPos.x+1, centerPos.y-1);
        occupiedCorners += IsOccupied(centerPos.x-1, centerPos.y-1);
        return occupiedCorners;
    }

    /// <summary>
    /// Counts how many of the T's two FRONT corners are filled — i.e. the corners
    /// adjacent to the side the stub points to. This is what distinguishes a
    /// proper T-spin (≥2 front corners filled) from a Mini T-spin (≤1 front).
    ///
    /// Rotation states (matches PieceScript._currentRotation):
    ///   0 = spawn  → stub points UP    → front corners = top-left,    top-right
    ///   1 = right  → stub points RIGHT → front corners = top-right,   bottom-right
    ///   2 = reverse→ stub points DOWN  → front corners = bottom-left, bottom-right
    ///   3 = left   → stub points LEFT  → front corners = top-left,    bottom-left
    /// </summary>
    private int GetFrontCornersOccupied(Vector2Int centerPos, int rotation)
    {
        int dxA, dyA, dxB, dyB;
        switch (rotation)
        {
            case 0:  dxA = -1; dyA =  1; dxB =  1; dyB =  1; break; // top corners
            case 1:  dxA =  1; dyA =  1; dxB =  1; dyB = -1; break; // right corners
            case 2:  dxA = -1; dyA = -1; dxB =  1; dyB = -1; break; // bottom corners
            default: dxA = -1; dyA =  1; dxB = -1; dyB = -1; break; // left corners (rot 3)
        }
        return IsOccupied(centerPos.x + dxA, centerPos.y + dyA)
             + IsOccupied(centerPos.x + dxB, centerPos.y + dyB);
    }

    /// <summary>
    /// Modified isAvailable method
    /// </summary>
    /// <returns>0 if available, 1 otherwise</returns>
    private int IsOccupied(int x, int y)
    {
        // Check if coordinates are out of bounds 
        if (x < 0 || x > 9 || y < 0)
            return 1;

        // Return true if the space is empty or the block is active
        bool isValid = _blocksInGrid[y][x] == null || _blocksInGrid[y][x].GetComponent<Block>().GetBlockStatus();
        return isValid ? 0 : 1;
    }

    /// <summary>
    /// This method clears full rows and shifts down the above rows accordingly
    /// </summary>
    /// <param name="fullRows"></param>
    public void ClearRows(List<int> fullRows, PieceInfo info)
    {
        // --- Track lines and derive level (1 level per 10 lines, capped at 15) ---
        // Matches play.tetris.com Marathon: 10 lines per level, max level 15.
        // The gravity formula in PieceController already caps speed around L15
        // (b = 0.8 - 0.007×(L-1) goes ≤ 0 past L≈115, so speed plateaus at the
        // 0.001s floor anyway), and Marathon ends at 150 lines on play.tetris.com.
        // Bump this if you want endless play with continued score multipliers.
        _linesCleared += fullRows.Count;
        _level = Mathf.Min(_linesCleared / 10 + 1, 15);

        // ---------- Clear Rows, Destroy Game Objects ----------
        for (int i = 0; i < fullRows.Count; i++)
        {
            int y = fullRows[i];
            Destroy(_parentRows[y]);
            _parentRows[y] = null;
            _blockCount[y] = 0;
        }

        List<Tuple<int, int, int>> rowsToShift = new List<Tuple<int, int, int>>(); // start row to shift, last row to shift, amount to shift
                                                                                   // Gather touples to give us clear information

        for (int i = 0; i < fullRows.Count; i++)
        {
            int y1 = fullRows[i] + 1; //1
            int y2 = (i + 1 < fullRows.Count) ? fullRows[i + 1] - 1 : _maxHeight - 1;

            if (y2 < y1)
            {
                continue; // rows are stacked, continue
            }
            //Debug.Log($"Row: {i}, y1: {y1}, y2: {y2}");
            rowsToShift.Add(new Tuple<int, int, int>(y1, y2, i + 1));
        }

        CalculcateScore(rowsToShift, info);

        ShiftRows(rowsToShift);
    }

    private void ShiftRows(List<Tuple<int, int, int>> rowsToShift)
    {
        //rowsToShift.Sort((a, b) => b.Item1.CompareTo(a.Item1));
        string str = "";
        for (int i = 0; i < rowsToShift.Count; i++)
        {
            str += $"Index {i}: {rowsToShift[i].Item1} to {rowsToShift[i].Item2}, shift: {rowsToShift[i].Item3}\n";
        }
        for (int i = 0; i < rowsToShift.Count; i++)
        {
            int y1 = rowsToShift[i].Item1;
            int y2 = rowsToShift[i].Item2;
            int shift = rowsToShift[i].Item3;

            for (int r = y1; r <= y2; r++)
            {
                if (_parentRows[r] == null)
                { //don't shift nonexistent rows
                    continue;
                }

                // Shift physical location
                ShiftUnityPosition(r, shift);

                // Shift blocks in the parent row grid
                _parentRows[r - shift] = _parentRows[r];
                _parentRows[r - shift].transform.name = $"Row_{r - shift}";
                _parentRows[r] = null;

                // Shift block count
                _blockCount[r - shift] = _blockCount[r];
                _blockCount[r] = 0; // Clear old row

                // Shift blocks in the gameobject grid
                ShiftArray(r, r - shift);
            }
        }
    }

    private void ShiftUnityPosition(int row, int rowsToShift)
    {
        //Debug.Log($"Shifting unity with, row: {row} and shift: {rowsToShift}");
        for (int c = 0; c < _cols; c++)
        {
            if (_blocksInGrid[row][c] == null)
            {
                continue; // Skip if no block in this position
            }
            Block block = _blocksInGrid[row][c].GetComponent<Block>();
            block.OffsetPosition(0,-rowsToShift);
        }
    }
    private void ShiftArray(int oldRow, int newRow)
    {
        for (int c = 0; c < _cols; c++)
        {
            _blocksInGrid[newRow][c] = _blocksInGrid[oldRow][c];
            _blocksInGrid[oldRow][c] = null; // Clear old row
        }
            
    }

    // -----------------------PRINT METHOD-----------------------

    // Prints grid represented with 0s and 1s
    public void PrintGrid()
    {
        String str = "";
        for (int r = _maxHeight - 1; r >= 0; r--)
        {
            for (int c = 0; c < _cols; c++)
            {
                GameObject block = _blocksInGrid[r][c];
                if (block != null)
                {
                    if (block.GetComponent<Block>().GetBlockStatus()) str += "1";
                    else str += "0";
                }
                else str += "_";
            }
            str += "\n";
        }
        Debug.Log(str);
    }

    public void PrintArray(GameObject[] arr)
    {
        String str = "";
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null)
            {
                str += i + ": " + arr[i].name;
            }
            else
            {
                str += i + ": -";
            }
            str += "\n";
        }
        Debug.Log(str);
    }
}
