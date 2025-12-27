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

    public void ResetScoreStreak()
    {
        _scoreStreak = 0;
    }
    public void IncrementScore()
    {
        _scoreStreak++;
    }

    private void CalculcateScore(List<Tuple<int, int, int>> rowsToShift, PieceInfo info)
    {
        int totalRowsCleared = 0;
        for (int i = 0; i < rowsToShift.Count; i++)
            totalRowsCleared += rowsToShift[i].Item3;
        Debug.Log($"Total Rows Cleared: {totalRowsCleared}");

        switch (totalRowsCleared)
        {
            case 1:
                Debug.Log("Single");
                break;
            case 2:
                int numCorners = GetCornersOccupied(info.centerPos);
                if (info.pieceType == PieceType.T && info.lastMoveRotate && numCorners == 3)
                    Debug.Log("T-Spin Double!");
                else
                    Debug.Log("Double");
                break;
            case 3:
                numCorners = GetCornersOccupied(info.centerPos);
                if (info.pieceType == PieceType.T && info.lastMoveRotate && numCorners >= 3)
                    Debug.Log("T-Spin Triple!");
                else
                    Debug.Log("Triple");
                break;
            case 4:
                Debug.Log("Tetris!");
                break;
            default:
                break;
        }
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
