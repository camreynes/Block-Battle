using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PieceSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] _tetrominoPrefab = new GameObject[2];
    [SerializeField] protected BlockGrid _grid;

    private float _timeToFall = 0.2f;
    private Piece_Script _currentPiece;

    public Vector2[] _positions;

    void Start()
    {
        SpawnPiece();
        StartCoroutine(DelayedStart());
    }

    private void SpawnPiece()
    {
        Debug.Log("Attempting to spawn Blocks");
        GameObject pieceObj = null;
        pieceObj = Instantiate(_tetrominoPrefab[Random.Range(0,2)]);
        _currentPiece = pieceObj.GetComponent<Piece_Script>();

        _currentPiece.SetGrid(_grid);

        Vector2[] _initialPositions = _currentPiece.GetInitialPositions();
        _positions = _initialPositions;
        _currentPiece.SetPositions(_initialPositions);

        bool canSpawn = _currentPiece.CheckBlockLocations(_positions);
        if (!canSpawn)
        {
            //Debug.Log("Can not spawn block");
            // Eventually this will be a condition to end the game
            return;
        }

        PrintVector2Array(_initialPositions);

        _currentPiece.SpawnBlocks(_initialPositions);
    }



    // Delay before the first block falls
    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(_timeToFall);
        StartCoroutine(BlockFall());         
    }

    // Update is called once per frame
    private IEnumerator BlockFall()
    {
        while (true)
        {
            if (_currentPiece == null)
            {
                yield return null; // wait one frame to ensure full setup
                continue;
            }

            // Create offset vectors (-1 y) and check if we can place there
            Vector2[] newPieceLocations = _currentPiece.CreateOffsetVectors(1, 0);
            bool canPlace = _currentPiece.CheckBlockLocations(newPieceLocations);

            if (canPlace)
            {
                _positions = newPieceLocations;
                _currentPiece.OffsetBlocks(1,0);
                //BlockGrid.PrintGrid(_grid);
            }
            else
            {
                // If we can't place the blocks, we need to spawn a new piece
                _currentPiece.SetBlocksInactive();
                _currentPiece = null;
            }

            if (_currentPiece == null)
            {
                SpawnPiece();
            }

            yield return new WaitForSeconds(_timeToFall); // fall every second
        }
    }

    public static void PrintVector2Array(Vector2[] vectors, string label = "Vector2 Array")
    {
        string result = label + ": [ ";
        foreach (Vector2 v in vectors)
        {
            result += $"({v.x}, {v.y}) ";
        }
        result += "]";
        Debug.Log(result);
    }
}
