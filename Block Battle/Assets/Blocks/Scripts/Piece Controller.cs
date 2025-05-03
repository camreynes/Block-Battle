using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

// Class that handles piece spawning, roations, movements, etc.
public class PieceController : MonoBehaviour
{
    [SerializeField] private GameObject[] _tetrominoPrefab = new GameObject[2];
    [SerializeField] protected BlockGrid _grid;

    private float _timeToFall = 0.8f;
    private PieceScript _currentPiece;
    private int playerId = 0; // Player ID for input mapping, will make dynamic later

    public Vector2[] _positions;

    private HoldState _holdLeft;
    private HoldState _holdRight;
    private HoldState _holdDown;


    // On Awake(), we define player inputs
    private void Awake()
    {
        // Hold States
        _holdLeft = new HoldState(0.167f, 0.033f);
        _holdRight = new HoldState(0.167f, 0.033f);
        _holdDown = new HoldState(0.05f, 0.02f);
    }

    // We use update to check for held keys
    private void Update()
    {
        // HOLDING ACCELERATED MOVEMENT
        bool leftHeld = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld = _holdDown.IsHolding;

        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
            TryMovePiece(new Vector2(0, -1));
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
            TryMovePiece(new Vector2(0, 1));
        if (downHeld && _holdDown.ShouldRepeat())
            TryMovePiece(new Vector2(1, 0));

        // PLAYER INPUTS
        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT, playerId))
            OnMoveStart(new Vector2(0,-1));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_LEFT, playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2(0, -1));

        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, playerId))
            OnMoveStart(new Vector2(0, 1));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_RIGHT, playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2(0, 1));

        if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP, playerId))
            OnMoveStart(new Vector2(1, 0));
        if (TetrixInputManager.GetInputAction(GameInputAction.SOFT_DROP, playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2(1, 0));
    }

    // Function to set hold states, only needed for keys that can be presse (move left, right and down)
    private void OnMoveStart(Vector2 direction)
    {
        Debug.Log($"Move started: {direction}");

        // Remember x and y are reffered to as poistions in the array, not unity coordinates
        if (direction.y < 0)
            _holdLeft.StartHold();
        else if (direction.y > 0)
            _holdRight.StartHold();
        else if (direction.x > 0)
            _holdDown.StartHold();
    }

    // Function to set hold states false, appended to move end
    private void OnMoveEnd(Vector2 direction)
    {
        Debug.Log($"Move ended: {direction}");
        if (direction.y < 0)
            _holdLeft.StopHold();
        else if (direction.y > 0)
            _holdRight.StopHold();
        else if (direction.x > 0)
            _holdDown.StopHold();
    }

    // This function checks if we can move the piece in the given direction and moves if we can
    private void TryMovePiece(Vector2 direction)
    {
        Vector2[] newPositions = _currentPiece.CreateOffsetVectors((int)direction.x, (int)direction.y);

        if (_currentPiece.CheckBlockLocations(newPositions))
            _currentPiece.OffsetBlocks((int)direction.x, (int)direction.y);
    }

    // Once inputs defined, we can start spawning pieces at an interval
    void Start()
    {
        SpawnPiece();
        StartCoroutine(DelayedStart());
    }

    private void SpawnPiece()
    {
        //Debug.Log("Attempting to spawn Blocks");
        GameObject pieceObj = null;
        pieceObj = Instantiate(_tetrominoPrefab[UnityEngine.Random.Range(0, 2)]);
        _currentPiece = pieceObj.GetComponent<PieceScript>();

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

        //PrintVector2Array(_initialPositions);

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
