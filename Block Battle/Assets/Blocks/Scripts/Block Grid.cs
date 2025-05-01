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
    private Vector2 _position = new Vector2(-2.24740648269653f, -4.47401666641235f); //represents bottom left position of the grid
    private int _rows = 20;
    private int _cols = 10;

    public Vector2 scale = new Vector2(0,0);

    private GameObject[,] blocksInGrid = new GameObject[20,10]; // Initialize array of gameObjects (cubes) representning the filled array

    private void Awake()
    {
        // Initialize the inner array to avoid null reference issues
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        // Get the bounds of the gameObject and find height and width
        SpriteRenderer gridRenderer = gameObject.GetComponent<SpriteRenderer>();
        Bounds gridBounds = gridRenderer.bounds;
        _position = new Vector2(gameObject.transform.position.x, gameObject.transform.position.y);

        double width = gridBounds.size.x / 10;
        double height = gridBounds.size.y / 20;
        // These values should match but do not due to Unity

        // Initialize private widh and height (We do this in case we use a custom grid rather than the default 20x10 one)
        _width = width;
        _height = height;

        scale = gameObject.transform.localScale;
    }

    // Prints grid represented with 0s and 1s
    public static void PrintGrid(BlockGrid grid) {
        String str = "";
        for (int r = 0; r < grid._rows; r++) {
            for (int c = 0; c < grid._cols; c++)
            {
                str += (grid.blocksInGrid[r, c] == null) ? "_" : "X";
            }
            str += "\n";
        }
        Debug.Log(str);
    }
        
    // Public Methods
    // Returns (bottom left) position in grid based on the row and column
    // Rows count up from 0 to 19, Columns left to right from 0 to 9
    public Vector3 GetPosInGrid(int row, int col) // May deprecate
    {
        return new Vector3((float)(_position.x + _width * col), (float)(_position.y + _height * row), 0);
    }

    // More Public Methods, used to be in block class
    // Method to change the grid array
    public void SetBlockInGridArray(GameObject block, int x, int y)
    {
        blocksInGrid[x, y] = block;
    }

    // Method to check if space is avaiable based on offset
    public bool CheckSpace(int x, int y)
    {
        if (x < 0 || y < 0) return false;

        GameObject objectAtLocation = blocksInGrid[_rows - 1 - x, y];
        if (objectAtLocation == null || objectAtLocation.GetComponent<Block>().isActive)
        {
            return true;
        }
        return false;
    }

}
