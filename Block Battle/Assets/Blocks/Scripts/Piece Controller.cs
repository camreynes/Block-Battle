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
    private float _timeToFall = 999999.8f;
    private float _lockDelay = .5f;
    private float _maxLockDelay = 1.5f;

    private PieceScript _currentPiece;
    private int _playerId = 0; // Player ID for input mapping, will make dynamic later

    private HoldState _holdLeft;
    private HoldState _holdRight;
    private HoldState _holdDown;

    private bool _recentlyMoved = false;
    private bool _forceHardDrop = false;
    private bool _stagepreset = true;

    private List<Vector2Int[]> _stagePreset = new List<Vector2Int[]>(); // stage presets if saved

    private Coroutine _fallRoutine;

    // On Awake(), we define player inputs
    private void Awake()
    {
        // Hold States
        _holdLeft = new HoldState(0.167f, 0.033f);
        _holdRight = new HoldState(0.167f, 0.033f);
        _holdDown = new HoldState(0.05f, 0.02f);
    }

    private void Start()
    {
        if (_stagepreset) {
            Global.GetPreset();
            GameObject pieceObj = Instantiate(_tetrominoPrefab[1]);
            _currentPiece = pieceObj.GetComponent<PieceScript>();
            _currentPiece.SetGrid(_grid);

            Vector2Int[] initialPositions = Global.scenePreset.ToArray();
            _currentPiece.SetPositions(initialPositions);
            _currentPiece.SpawnBlocks(initialPositions);
            _currentPiece.SetBlocksInactive();
        }

        SpawnPiece();
        _fallRoutine = StartCoroutine(BlockFall());
    }

    // We use update to check for held keys
    private void Update()
    {
        //if (_recentlyMoved)
        //    Debug.Log("Moved");
        // HOLDING ACCELERATED MOVEMENT
        _recentlyMoved = false;
        bool leftHeld = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld = _holdDown.IsHolding;

        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(new Vector2Int(0, -1));
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(new Vector2Int(0, 1));
        if (downHeld && _holdDown.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(new Vector2Int(1, 0));

        // PLAYER INPUTS - MOVEMENT
        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT, _playerId))
            OnMoveStart(new Vector2Int(0, -1));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_LEFT, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(0, -1));

        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, _playerId))
            OnMoveStart(new Vector2Int(0, 1));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_RIGHT, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(0, 1));

        if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP, _playerId))
            OnMoveStart(new Vector2Int(1, 0));
        if (TetrixInputManager.GetInputAction(GameInputAction.SOFT_DROP, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(1, 0));

        if (TetrixInputManager.WasPressed(GameInputAction.HARD_DROP, _playerId))
            HardDrop();

        // PLAYER INPUTS - ROTATIONS
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CW, _playerId))
            _recentlyMoved = _currentPiece.TryRotateCW();
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CCW, _playerId))
            _recentlyMoved = _currentPiece.TryRotateCCW();

        // PLAYER INPUTS - TESTING
        if (TetrixInputManager.WasPressed(GameInputAction.SAVE_SCENE, _playerId))
            _currentPiece.PrintScene();
    }

    // Function to set hold states, only needed for keys that can be presse (move left, right and down)
    private void OnMoveStart(Vector2Int direction)
    {
        //Debug.Log($"Move started: {direction}");

        // Remember x and y are reffered to as poistions in the array, not unity coordinates
        if (direction.y < 0)
            _holdLeft.StartHold();
        else if (direction.y > 0)
            _holdRight.StartHold();
        else if (direction.x > 0)
            _holdDown.StartHold();
        _recentlyMoved = true;
    }

    // Function to set hold states false, appended to move end
    private void OnMoveEnd(Vector2Int direction)
    {
        //Debug.Log($"Move ended: {direction}");
        if (direction.y < 0)
            _holdLeft.StopHold();
        else if (direction.y > 0)
            _holdRight.StopHold();
        else if (direction.x > 0)
            _holdDown.StopHold();
    }

    private void SpawnPiece()
    {
        _forceHardDrop = false;

        //Debug.Log("Attempting to spawn Blocks");
        GameObject pieceObj = null;
        pieceObj = Instantiate(_tetrominoPrefab[UnityEngine.Random.Range(1, 2)]);
        _currentPiece = pieceObj.GetComponent<PieceScript>();
        _currentPiece.SetGrid(_grid);

        Vector2Int[] initialPositions = _currentPiece.GetInitialPositions();
        //Debug.Log($"Initial positions from PieceController:");
        //PrintVector2Array(_initialPositions);

        _currentPiece.SetPositions(initialPositions);

        bool canSpawn = _currentPiece.CheckBlockLocations(initialPositions);
        if (!canSpawn)
        {
            //Debug.Log("Can not spawn block");
            // Eventually this will be a condition to end the game
            return;
        }

        //PrintVector2Array(_initialPositions);
        _currentPiece.SpawnBlocks(initialPositions);
    }

    private void HardDrop()
    {
        _forceHardDrop = true;
        _currentPiece?.HardDrop();
        _currentPiece?.SetBlocksInactive();
        _currentPiece = null;
    }

    private IEnumerator BlockFall()
    {
        while (true)
        {
            if (_currentPiece == null)
            {
                SpawnPiece();
                yield return null; // wait one frame to ensure full setup
                continue;
            }

            bool canMoveDown = _currentPiece.TestOffset(new Vector2Int(1, 0)); // Can we place below?

            if (canMoveDown) // Normal falling, using a timer here instead of waitForSeconds so it can be interrupted
            {
                yield return WaitAndFall();
            }

            else // Lock Delay
            {
                yield return LockDelay();
            }

        }
    }

    private IEnumerator WaitAndFall()
    {
        float timer = 0f;
        while (timer < _timeToFall && !_forceHardDrop)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (!_forceHardDrop)
            _currentPiece.TryMovePiece(new Vector2Int(1, 0));
    }

    private IEnumerator LockDelay()
    {
        float timer = 0;
        bool canMoveDown = false;
        float lastLockDelay = 0; // add the differencv between the last lock delay and the current one
        float currentMaxtime = Math.Min(_timeToFall, _lockDelay);
        bool newMovePossible = false;

        while (timer < currentMaxtime && !_forceHardDrop)
        {
            Debug.Log(timer);
            if (_recentlyMoved && !newMovePossible)
            {
                newMovePossible = canMoveDown = _currentPiece.TestOffset(new Vector2Int(1, 0));
                currentMaxtime = Math.Min(currentMaxtime + _lockDelay - lastLockDelay, _maxLockDelay);
                lastLockDelay = .5f;
            }
            timer += Time.deltaTime;
            lastLockDelay = Math.Max(0f, lastLockDelay - Time.deltaTime);
            yield return null;
        }
        if (canMoveDown && !_forceHardDrop)
        {
            _currentPiece.TryMovePiece(new Vector2Int(1, 0));
        }
        else
        {
            _currentPiece?.SetBlocksInactive();
            _currentPiece = null;
        }
    }

    public void SetGrid(BlockGrid grid)
    {
        _grid = grid;
    }

    public static void PrintVector2Array(Vector2Int[] vectors, string label = "Vector2 Array")
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
