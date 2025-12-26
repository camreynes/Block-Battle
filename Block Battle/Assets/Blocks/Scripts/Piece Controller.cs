using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Class that handles piece spawning, roations, movements, etc.
public class PieceController : MonoBehaviour
{

    [SerializeField] private GameObject[] _tetrominoPrefab;
    [SerializeField] protected BlockGrid _grid;

    private List<Tuple<int, GameObject>> _pieceOrder = new List<Tuple<int, GameObject>>();
    private List<Tuple<int, GameObject>> _tempPieceList = new List<Tuple<int, GameObject>>();

    private Vector2Int[] _lastPositions;

    private float _timeToFall = .8f;
    private float _lockDelay = .5f;
    private float _maxLockDelay = 1.5f;

    private PieceScript _currentPiece;
    private int _playerID = -1; // Player ID for input mapping, will make dynamic later
    private int _spawnHeld = -1; // To determine if we are spawning a held piece, if so what piece type

    private HoldState _holdLeft;
    private HoldState _holdRight;
    private HoldState _holdDown;

    private bool _recentlyMovedByPlayer = false;
    private bool _recentlyRotatedByPlayer = false;
    private bool _lastMoveRotate = false; // false = move, true = rotate (mostly for score detection)
    private bool _forceHardDrop = false;
    private bool _recentlyHeld = false; // To prevent multiple holds in one turn
    [SerializeField] private bool _setPiece = false; // Used to determine if we are using only one hardset piece
    [SerializeField] private bool _stagePreset = false;

    private Preview _preview;
    private Outline _outline;
    private Hold _hold;

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
        if (_stagePreset)
        {  // Special conditions if we are using a preset stage
            Global.GetPreset();
            GameObject pieceObj = Instantiate(_tetrominoPrefab[1]);
            _currentPiece = pieceObj.GetComponent<PieceScript>();
            _currentPiece.SetGrid(_grid);

            Vector2Int[] initialPositions = Global.scenePreset.ToArray();
            _currentPiece.SetPositions(initialPositions);
            _currentPiece.SpawnBlocks(initialPositions);

            _currentPiece.SetBlocksInactive(gameObject);
            PieceInfo _pieceInfo = new PieceInfo(_currentPiece.GetPieceType(), _lastMoveRotate);
            _currentPiece.FinishDestory(_pieceInfo);
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

        var currentPositions = _currentPiece.GetPositions();
        if ((_recentlyMovedByPlayer || _recentlyRotatedByPlayer) && _lastPositions != null && !_lastPositions.SequenceEqual(currentPositions))
        {
            // piece actually changed grid cells (move or rotate)
            _outline.UpdateOutline(_currentPiece.GetOutlineVectors(), _currentPiece.GetPieceType());
        }
        _lastPositions = currentPositions; // keep the latest snapshot


        //if (_recentlyMoved)
        //    Debug.Log("Moved");
        // HOLDING ACCELERATED MOVEMENT
        _recentlyMovedByPlayer = false;
        _recentlyRotatedByPlayer = false;  
        bool leftHeld = _holdLeft.IsHolding;
        bool rightHeld = _holdRight.IsHolding;
        bool downHeld = _holdDown.IsHolding;

        if (leftHeld && !rightHeld && _holdLeft.ShouldRepeat())
            _recentlyMovedByPlayer = _currentPiece.TryMovePiece(LEFT);
        else if (rightHeld && !leftHeld && _holdRight.ShouldRepeat())
            _recentlyMovedByPlayer = _currentPiece.TryMovePiece(RIGHT);
        if (downHeld && _holdDown.ShouldRepeat())
            _recentlyMovedByPlayer = _currentPiece.TryMovePiece(DOWN);

        // PLAYER INPUTS - MOVEMENT
        var actLeft = TetrixInputManager.GetInputAction(GameInputAction.MOVE_LEFT, _playerID);
        var actRight = TetrixInputManager.GetInputAction(GameInputAction.MOVE_RIGHT, _playerID);
        var actDown = TetrixInputManager.GetInputAction(GameInputAction.SOFT_DROP, _playerID);

        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_LEFT, _playerID))
            OnMoveStart(LEFT);
        if (actLeft != null && actLeft.WasReleasedThisFrame())
            OnMoveEnd(LEFT);

        if (TetrixInputManager.WasPressed(GameInputAction.MOVE_RIGHT, _playerID))
            OnMoveStart(RIGHT);
        if (actRight != null && actRight.WasReleasedThisFrame())
            OnMoveEnd(RIGHT);

        if (TetrixInputManager.WasPressed(GameInputAction.SOFT_DROP, _playerID))
            OnMoveStart(DOWN);
        if (actDown != null && actDown.WasReleasedThisFrame())
            OnMoveEnd(DOWN);

        if (TetrixInputManager.WasPressed(GameInputAction.HARD_DROP, _playerID))
            HardDrop();

        // PLAYER INPUTS - ROTATIONS
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CW, _playerID))
            _recentlyRotatedByPlayer = _currentPiece.TryRotateCW();
        if (TetrixInputManager.WasPressed(GameInputAction.ROTATE_CCW, _playerID))
            _recentlyRotatedByPlayer = _currentPiece.TryRotateCCW();

        // PLAYER INPUTS - HOLD
        if (TetrixInputManager.WasPressed(GameInputAction.HOLD, _playerID))
        {
            HoldPiece();
        }

        // PLAYER INPUTS - TESTING
        if (TetrixInputManager.WasPressed(GameInputAction.SAVE_SCENE, _playerID))
        {
            SaveScene();
        }

        if (_recentlyMovedByPlayer)
            _lastMoveRotate = false;
        if (_recentlyRotatedByPlayer)
            _lastMoveRotate = true;
    }

    // -----------------------PRIVATE HELPERS-----------------------



    // -----------------------PIECE ORDER-----------------------


    /// <summary> Creates a new order of pieces to be spawned. </summary>
    private void CreateNewOrder()
    {
        _tempPieceList.Clear();

        for (int i = 0; i < _tetrominoPrefab.Length; i++) // Copy of list to determine randomized order
        {
            _tempPieceList.Add(new Tuple<int, GameObject>(i, _tetrominoPrefab[i]));
        }

        for (int i = 0; i < _tetrominoPrefab.Length; i++) // Add to pieceOrder list
        {
            int randomIndex = UnityEngine.Random.Range(0, _tempPieceList.Count);
            _pieceOrder.Add(new Tuple<int, GameObject>(_tempPieceList[randomIndex].Item1, _tempPieceList[randomIndex].Item2));
            _tempPieceList.RemoveAt(randomIndex);
        }
    }

    // -----------------------MOVE HELPERS-----------------------
    private void HardDrop()
    {
        _forceHardDrop = true;
        _currentPiece?.HardDrop();
        SetBlocksInactive();
    }

    private void HoldPiece()
    {
        if (_recentlyHeld || _currentPiece == null) // Prevent multiple holds in one turn
            return;
        _recentlyHeld = true;
        _spawnHeld = _hold.UpdateHold((int)_currentPiece.GetPieceType());
        Destroy(_currentPiece.gameObject);
        _currentPiece = null;
    }

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
        _recentlyMovedByPlayer = true;
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
        if (_pieceOrder.Count <= 5)
            CreateNewOrder();
        if (_spawnHeld >= 0) // If we are using a set piece, we only spawn one piece
        {
            pieceObj = Instantiate(_tetrominoPrefab[_spawnHeld]);
            _spawnHeld = -1;
        }
        else if (_setPiece) // If we are using a set piece, we only spawn one piece
            pieceObj = Instantiate(_tetrominoPrefab[4]);
        else
        {
            pieceObj = Instantiate(_pieceOrder[0].Item2);
            _pieceOrder.RemoveAt(0);
        }

        // Update preview with next pieces
        int[] list = new int[] { _pieceOrder[0].Item1, _pieceOrder[1].Item1, _pieceOrder[2].Item1, _pieceOrder[3].Item1 };
        _preview.UpdatePreview(list);


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
        _outline.UpdateOutline(_currentPiece.GetOutlineVectors(), _currentPiece.GetPieceType()); //extra call to update outline on spawn
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
        while (timer < _timeToFall && !_forceHardDrop && _currentPiece != null)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        if (!_forceHardDrop && _currentPiece != null)
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
            if ((_recentlyMovedByPlayer || _recentlyRotatedByPlayer) && !newMovePossible)
            {
                newMovePossible = canMoveDown = _currentPiece.TestOffset(new Vector2Int(0, -1));
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


    /// <summary>
    /// Sets the blocks of the current piece to inactive and handles destruction if needed.
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator SetBlocksInactive()
    {
        if (_currentPiece == null)
            yield break; // No current piece to set inactive


        bool isFull = _currentPiece.SetBlocksInactive(gameObject);
        if (isFull)
        {
            yield return new WaitForSeconds(Global.effectDuration); // suspends the coroutine for duration of effect
            _currentPiece.FinishDestory();
        }

        _currentPiece = null;
        _recentlyHeld = false; // Reset hold ability

        yield break;
    }

    // -----------------------PUBLIC METHODS-----------------------

    public void SetGrid(BlockGrid grid) { _grid = grid; }

    public void SetHold(Hold hold) { _hold = hold; ; }

    public void SetPreview(Preview preview) { _preview = preview; }

    public void SetOutline(Outline outline) { _outline = outline; }

    public void SetPlayerID(int id) { _playerID = id; }

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
