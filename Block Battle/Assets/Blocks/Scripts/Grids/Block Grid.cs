using System;
using System.Collections.Generic;
using System.Numerics;
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
    private Vector2 _position; //represents bottom left position of the grid
    private int _rows = 20;
    private int _cols = 10;
    private int _playerID;

    public Vector2 scale = new Vector2(0,0);

    private int _maxHeight = 20;
    private int _spaceUsed = 0; // Tracks the top most block being used
    private GameObject[,] _blocksInGrid = new GameObject[20, 10]; // Initialize array of gameObjects (cubes) representning the filled array
    private GameObject[] _parentRows = new GameObject[20]; // Tracks the number of blocks in each row
    private int[] _blockCount = new int[20]; // Tracks the number of blocks in each row

    //private void Awake()
    //{
    //    // Initialize the inner array to avoid null reference issues
    //    InitializeGrid();
    //}

    public void InitializeSelf(int playerID)
    {
        // Get the bounds of the gameObject and find height and width
        SpriteRenderer gridRenderer = gameObject.GetComponent<SpriteRenderer>();
        Bounds gridBounds = gridRenderer.bounds;
        _position = new Vector2(transform.position.x, transform.position.y); // Get the position of the grid
        //Debug.Log($"Grid position: {_position}");

        double width = gridBounds.size.x / _cols;
        double height = gridBounds.size.y / _rows;
        // These values should match but do not due to Unity

        // Initialize private widh and height (We do this in case we use a custom grid rather than the default 20x10 one)
        _width = width;
        _height = height;
        _playerID = playerID;

        scale = gameObject.transform.localScale;
        //Debug.Log($"Grid height: {height}, width: {width}");

        Debug.DrawLine(GetPosInGrid(0, 0), GetPosInGrid(0, 0) + Vector3.up * 0.5f, Color.green, 5f);
        Debug.DrawLine(GetPosInGrid(0, 0), GetPosInGrid(0, 0) + Vector3.right * 0.5f, Color.red, 5f);

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

    public bool IsRowEmpty(int y)
    {
        return _parentRows[y] == null;
    }

    // More Public Methods, used to be in block class
    // Method to change the grid array
    public void SetBlockInGridArray(GameObject block, int x, int y)
    {
        _blocksInGrid[y, x] = block;
        if (y >= _spaceUsed)
            _spaceUsed = y;
    }

    public void IncrementBlockCount(int y)
    {
        _blockCount[y]++;
    }

    // -----------------------CHECKING METHODS-----------------------


    // Method to check if space is avaiable based on offset, long method for debug atm
    public bool CheckSpace(int x, int y)
    {
        // Check if coordinates are out of bounds 
        if (x < 0 || x > 9 || y < 0 || y > _maxHeight-1)
            return false;

        // Return true if the space is empty or the block is active
        bool isValid = _blocksInGrid[y, x] == null || _blocksInGrid[y, x].GetComponent<Block>().GetBlockStatus();
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

    public List<Tuple<int, int, int>> ClearRows(List<int> fullRows)
    {
        // ---------- Clear Rows, Destroy Game Objects ----------
        for (int i = 0; i < fullRows.Count; i++) 
        {
            ClearRow(fullRows[i]);
        }

        List<Tuple<int, int, int>> rowsToClear = new List<Tuple<int, int, int>>(); // start row to shift, last row to shift, amount to shift
        // Gather touples to give us clear information
        
        for (int i = 0; i < fullRows.Count; i++)
        {
            int y1 = fullRows[i] + 1; //1
            int y2 = (i+1 < fullRows.Count) ? fullRows[i+1]-1 : _maxHeight-1;
            
            //Debug.Log($"Row: {i}, y1: {y1}, y2: {y2}");

            if (y2 < y1)
            {
                continue; // rows are stacked, continue
            }
            rowsToClear.Add(new Tuple<int, int, int>(y1,y2,i+1));
        }
        return rowsToClear;
    }


    // -----------------------CLEARING METHODS-----------------------

    public void ClearRow(int row)
    {
        for (int c = 0; c < _cols; c++) {
            Destroy(_blocksInGrid[row, c]); // Potentially a place to do object pooling here
            _blocksInGrid[row, c] = null;
        }
        _blockCount[row] = 0;
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
                GameObject block = _blocksInGrid[r, c];
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
}
