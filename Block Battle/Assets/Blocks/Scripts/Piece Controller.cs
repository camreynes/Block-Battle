using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Class that handles piece spawning, roations, movements, etc.
public class PieceController : MonoBehaviour
{
    
    [SerializeField] private GameObject[] _tetrominoPrefab;
    [SerializeField] protected BlockGrid _grid;

    private List<GameObject> _pieceOrder = new List<GameObject>();
    private List<GameObject> _tempPieceList = new List<GameObject>();

    private float _timeToFall = .8f;
    private float _lockDelay = .5f;
    private float _maxLockDelay = 1.5f;

    private PieceScript _currentPiece;
    private int _playerId = 0; // Player ID for input mapping, will make dynamic later

    private HoldState _holdLeft;
    private HoldState _holdRight;
    private HoldState _holdDown;

    private bool _recentlyMoved = false;
    private bool _forceHardDrop = false;
    [SerializeField] private bool _setPiece = false; // Used to determine if we are using only one hardset piece
    [SerializeField] private bool _stagePreset = false;

    private static readonly Vector2Int LEFT = new Vector2Int(-1, 0);
    private static readonly Vector2Int RIGHT = new Vector2Int(1, 0);
    private static readonly Vector2Int DOWN = new Vector2Int(0, -1);


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
        if (_stagePreset) {  // Special conditions if we are using a preset stage
            Global.GetPreset();
            GameObject pieceObj = Instantiate(_tetrominoPrefab[1]);
            _currentPiece = pieceObj.GetComponent<PieceScript>();
            _currentPiece.SetGrid(_grid);

            Vector2Int[] initialPositions = Global.scenePreset.ToArray();
            _currentPiece.SetPositions(initialPositions);
            _currentPiece.SpawnBlocks(initialPositions);

            _currentPiece.SetBlocksInactive(gameObject);
            _currentPiece.FinishDestory();
            _currentPiece = null;
        }

        SpawnPiece();
        _fallRoutine = StartCoroutine(BlockFall());
    }

    // We use update to check for held keys
    private void Update()
    {
        if (_currentPiece == null)
            return;
        //if (_recentlyMoved)
        //    Debug.Log("Moved");
        // HOLDING ACCELERATED MOVEMENT
        _recentlyMoved = false;
        bool leftHeld = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld = _holdDown.IsHolding;

        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(LEFT);
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(RIGHT);
        if (downHeld && _holdDown.ShouldRepeat())
            _recentlyMoved = _currentPiece.TryMovePiece(DOWN);

        // PLAYER INPUTS - MOVEMENT
        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT, _playerId))
            OnMoveStart(new Vector2Int(-1, 0));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_LEFT, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(-1, -0));

        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, _playerId))
            OnMoveStart(new Vector2Int(1, 0));
        if (TetrixInputManager.GetInputAction(GameInputAction.MOVE_RIGHT, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(1, 0));

        if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP, _playerId))
            OnMoveStart(new Vector2Int(0, -1));
        if (TetrixInputManager.GetInputAction(GameInputAction.SOFT_DROP, _playerId).WasReleasedThisFrame())
            OnMoveEnd(new Vector2Int(0, -1));

        if (TetrixInputManager.WasPressed(GameInputAction.HARD_DROP, _playerId))
            HardDrop();

        // PLAYER INPUTS - ROTATIONS
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CW, _playerId))
            _recentlyMoved = _currentPiece.TryRotateCW();
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CCW, _playerId))
            _recentlyMoved = _currentPiece.TryRotateCCW();

        // PLAYER INPUTS - TESTING
        if (TetrixInputManager.WasPressed(GameInputAction.SAVE_SCENE, _playerId)) {
            SaveScene();
        }
    }

    // -----------------------PRIVATE HELPERS-----------------------

    // -----------------------PIECE ORDER-----------------------


    /// <summary> Creates a new order of pieces to be spawned. </summary>
    private void CreateNewOrder()
    {
        _tempPieceList.Clear();

        for (int i = 0; i < _tetrominoPrefab.Length; i++) // Copy of list to determine randomized order
        {
            _tempPieceList.Add(_tetrominoPrefab[i]);
        }

        for (int i = 0; i < _tetrominoPrefab.Length; i++) // Add to pieceOrder list
        {
            int randomIndex = UnityEngine.Random.Range(0, _tempPieceList.Count);
            _pieceOrder.Add(_tempPieceList[randomIndex]);
            _tempPieceList.RemoveAt(randomIndex);
        }
    }

    // -----------------------MOVE HELPERS-----------------------

    // Function to set hold states, only needed for keys that can be presse (move left, right and down)
    private void OnMoveStart(Vector2Int direction)
    {
        //Debug.Log($"Move started: {direction}");

        // Remember x and y are reffered to as poistions in the array, not unity coordinates
        if (direction.x < 0)
            _holdLeft.StartHold();
        else if (direction.x > 0)
            _holdRight.StartHold();
        else if (direction.y < 0)
            _holdDown.StartHold();
        _recentlyMoved = true;
    }

    // Function to set hold states false, appended to move end
    private void OnMoveEnd(Vector2Int direction)
    {
        //Debug.Log($"Move ended: {direction}");
        if (direction.x < 0)
            _holdLeft.StopHold();
        else if (direction.x > 0)
            _holdRight.StopHold();
        else if (direction.y < 0)
            _holdDown.StopHold();
    }

    // -----------------------SPAWN PIECE-----------------------

    private void SpawnPiece()
    {
        _forceHardDrop = false;
        GameObject pieceObj = null;

        // Random(ish) Piece Order
        if (_pieceOrder.Count <= 2)
            CreateNewOrder();

        if (_setPiece) // If we are using a set piece, we only spawn one piece
        {
            pieceObj = Instantiate(_tetrominoPrefab[5]);
        }
        else
        {
            pieceObj = Instantiate(_pieceOrder[0]);
            _pieceOrder.RemoveAt(0);
        }
            

        _currentPiece = pieceObj.GetComponent<PieceScript>();
        _currentPiece.SetGrid(_grid);
        Vector2Int[] initialPositions = _currentPiece.GetInitialPositions();

        _currentPiece.SetPositions(initialPositions);

        bool canSpawn = _currentPiece.CheckBlockLocations(initialPositions);
        if (!canSpawn)
        {
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
        SetBlocksInactive();
    }

    // -----------------------PRIVATE IENUMERATOR (AND HELPERS)-----------------------

    private IEnumerator BlockFall()
    {
        while (true)
        {
            if (_currentPiece == null)
            {
                yield return .05;
                SpawnPiece();
                continue;
            }

            bool canMoveDown = _currentPiece.TestOffset(new Vector2Int(0, -1)); // Can we place below?

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
            _currentPiece.TryMovePiece(DOWN);
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
            //Debug.Log(timer);
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
            _currentPiece.TryMovePiece(new Vector2Int(0, -1));
        }
        else
        {
            yield return SetBlocksInactive();
        }
    }

    private IEnumerator SetBlocksInactive()
    {
        if (_currentPiece == null)
            yield return null; // No current piece to set inactive

        
        bool isFull = _currentPiece.SetBlocksInactive(gameObject);
        if (isFull)
        {
            yield return new WaitForSeconds(Global.effectDuration);
            _currentPiece.FinishDestory();
        }

        _currentPiece = null;
        yield return null;
    }

    // -----------------------PUBLIC METHODS-----------------------

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

    public void SaveScene()
    {
        Debug.Log($"Scene Saved with {Global.scenePreset.Count} Blocks");
        SaveUtility.Save(Global.scenePreset);
    }
}
