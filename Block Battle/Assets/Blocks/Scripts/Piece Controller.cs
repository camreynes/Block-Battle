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
    private PlayerInput _input;

    public Vector2[] _positions;

    private Dictionary<string, Vector2> _keyToDirection = new Dictionary<string, Vector2>();
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

        // Create a new PlayerInput object and enable the Player1Gameplay action map, scaling for later players
        _input = new PlayerInput();
        _input.Player1Gameplay.Enable();
        InitializeKeyDirectionMap(_input.Player1Gameplay.Move);

        // Add listeners to the hold states
        _input.Player1Gameplay.Move.started += OnMoveStart;
        _input.Player1Gameplay.Move.canceled += OnMoveEnd;
        _input.Player1Gameplay.SoftDrop.started += OnMoveStart;
        _input.Player1Gameplay.SoftDrop.canceled += OnMoveEnd;

        // Add liseners to the move action
        _input.Player1Gameplay.Move.performed += ApplyMove; // append function, (on finish returns context to onMove)
    }


    // Function to initialize the key to direction map, helpful to dynamically convery Unity bindings to directions
    // Unforunately, we need this method because Unity does not provide a way to get the key binding from the InputAction
    private Dictionary<string, Vector2> InitializeKeyDirectionMap(InputAction inputActions)
    {
        foreach (var binding in inputActions.bindings)
        {
            // Only care about part of 2D composite bindings, i.e. no duplicates
            if (!binding.isPartOfComposite) continue;

            Vector2 dir = Vector2.zero;
            switch (binding.name)
            {
                case "up": dir = Vector2.up; break;
                case "down": dir = Vector2.down; break;
                case "left": dir = Vector2.left; break;
                case "right": dir = Vector2.right; break;
            }

            // Add all control paths mapped to that direction
            if (dir != Vector2.zero)
            {
                var cleanPath = binding.path.Replace("<", "").Replace(">", "");
                _keyToDirection[$"/{cleanPath}"] = dir;
                Debug.Log($"Binding: /{cleanPath} -> Direction: {dir}");
            }
        }

        return null;
    }

    // Function to set hold states true, appended to move start
    private void OnMoveStart(InputAction.CallbackContext context)
    {
        Vector2 direction = context.ReadValue<Vector2>();
        Debug.Log($"Move started: {direction}");

        if (direction.x < 0)
            _holdLeft.StartHold();
        else if (direction.x > 0)
            _holdRight.StartHold();


        else if (direction.y < 0)
            _holdDown.StartHold();
    }

    // Function to set hold states false, appended to move end
    private void OnMoveEnd(InputAction.CallbackContext context)
    {

        Vector2 direction = _keyToDirection[context.control.path];

        Debug.Log($"Move ended: {direction} and {context.control.path}");

        if (direction.x != 0)
        {
            _holdLeft.StopHold();
            _holdRight.StopHold();
        }
        else if (direction.y != 0)
        {
            _holdDown.StopHold();
        }

    }

    // Function appended to the MOVE section of player input (only left right soft down)
    private void ApplyMove(InputAction.CallbackContext context)
    {
        Vector2 direction = context.ReadValue<Vector2>();

        if (direction.x < 0) TryMovePiece(new Vector2(0,-1));
        else if (direction.x > 0) TryMovePiece(new Vector2(0, 1));
        else if (direction.y < 0) TryMovePiece(new Vector2(1, 0));
    }


    // We use update to check for held keys
    private void Update()
    {
        bool leftHeld = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld = _holdDown.IsHolding;

        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
            TryMovePiece(new Vector2(0, -1));
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
            TryMovePiece(new Vector2(0, 1));

        if (downHeld && _holdDown.ShouldRepeat())
            TryMovePiece(new Vector2(1, 0));
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
        pieceObj = Instantiate(_tetrominoPrefab[1]);
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
