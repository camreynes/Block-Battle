using System;
using System.Numerics;
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

    private static int _maxHieght = 20;
    private GameObject[,] _blocksInGrid = new GameObject[_maxHieght, 10]; // Initialize array of gameObjects (cubes) representning the filled array

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

    // Prints grid represented with 0s and 1s
    public static void PrintGrid(BlockGrid grid) {
        String str = "";
        for (int r = _maxHieght - 1; r >= 0; r--) {
            for (int c = 0; c < grid._cols; c++)
            {
                GameObject block = grid._blocksInGrid[r, c];
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
        
    // Public Methods
    // Returns (bottom left) position in grid based on the row and column
    // Rows count up from 0 to 19, Columns left to right from 0 to 9
    public Vector3 GetPosInGrid(int x, int y)
    {
        //Debug.Log($"{_position}");
        return new Vector3((float)(_position.x + _width * x), (float)(_position.y + _height * y), 0);
    }
    public int getPlayerID()
    {
        return _playerID;
    }

    // More Public Methods, used to be in block class
    // Method to change the grid array
    public void SetBlockInGridArray(GameObject block, int x, int y)
    {
        _blocksInGrid[y, x] = block;
    }

    // Method to check if space is avaiable based on offset, long method for debug atm
    public bool CheckSpace(int x, int y)
    {
        // Check if coordinates are out of bounds 
        if (x < 0 || x > 9 || y < 0 || y > _maxHieght-1)
        { // Check if the coordinates are out of bounds}
            //Debug.Log($"Coordinates {x}, {y} are out of bounds");
            return false;
        }

        // Return true if the space is empty or the block is active
        bool isValid = _blocksInGrid[y, x] == null || _blocksInGrid[y, x].GetComponent<Block>().GetBlockStatus();
        if (isValid)
        {
            //Debug.Log($"Coordinates {x}, {y} are valid");
        }
        else
        {
            //Debug.Log($"Coordinates {x}, {y} are not valid {blocksInGrid[x, y].GetComponent<Block>().GetBlockStatus()}");
        }
        return isValid;
    }

}
