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

    private void Awake()
    {
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

        //Transform transformBlock = FindFirstObjectByType<Block>().gameObject.transform;
        //transformBlock.position = new Vector3(_position.x, _position.y,gameObject.transform.position.z);
        //Debug.Log($"x: {transformBlock.position.x} y: {transformBlock.position.y} !");
    }

    // Update is called once per frame
    private void Update()
    {

    }

    // Public Methods
    // Returns (bottom left) position in grid based on the row and column
    // Rows count up from 0 to 19, Columns left to right from 0 to 9
    public Vector3 GetPosInGrid(int row, int col)
    {
        return new Vector3((float)(_position.x + _width * row), (float)(_position.y + _height * row), 0);
    }
}
