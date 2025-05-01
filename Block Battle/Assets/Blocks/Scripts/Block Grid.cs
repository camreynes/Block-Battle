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

    public GameObject[,] blocksInGrid = new GameObject[20,10]; // Initialize array of gameObjects (cubes) representning the filled array

    private void Awake()
    {
        // Initialize the inner array to avoid null reference issues
        initializeGrid();
    }

    private void initializeGrid()
    {
        // Get the bounds of the gameObject and find height and width
        SpriteRenderer gridRenderer = gameObject.GetComponent<SpriteRenderer>();
        Bounds gridBounds = gridRenderer.bounds;
        _position = new Vector2(gameObject.transform.position.x, gameObject.transform.position.y);

        double width = gridBounds.size.x / 10;
        double height = gridBounds.size.y / 20;
        //Debug.Log($"Width: {width}, Height: {height}");
        // These values should match but do not due to Unity

        // Initialize private widh and height (We do this in case we use a custom grid rather than the default 20x10 one)
        _width = width;
        _height = height;

        scale = gameObject.transform.localScale;
        //Debug.Log($"x: {scale.x} y: {scale.y} !");

        //Transform transformBlock = FindFirstObjectByType<Block>().gameObject.transform;
        //transformBlock.position = new Vector3(_position.x, _position.y,gameObject.transform.position.z);
        //Debug.Log($"x: {transformBlock.position.x} y: {transformBlock.position.y} !");
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
    public static Vector3 GetPosInGrid(BlockGrid grid, int row, int col) // May deprecate
    {
        return new Vector3((float)(grid._position.x + grid._width * col), (float)(grid._position.y + grid._height * row), 0);
    }

    // This method doesn't actuallty place the GameObject in grid but rather
    //  returns position like GetPosInGrid() and updates the gameObject array
    public static Vector3 PlaceObjectInGrid(BlockGrid grid, GameObject block, int row, int col) {
        Vector3 returnVector = new Vector3((float)(grid._position.x + grid._width * col), (float)(grid._position.y + grid._height * row), 0);
        row = 19 - row;
        grid.blocksInGrid[row, col] = block;
        return returnVector;
    }


}
