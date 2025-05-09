using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceScript : MonoBehaviour
{
    [SerializeField] protected GameObject _blockPrefab;
    PieceType _pieceType;

    private BlockGrid _blockGrid;
    private GameObject[] _blocks;

    private Vector2Int[] _positions;
    private int _currentRotation = 0; // 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees

    public void SetGrid(BlockGrid grid)
    {
        Debug.Log($"Setting grid to {grid.gameObject.name}");
        _blockGrid = grid;
    }

    // -----------------------INITILIZATION AND CHECKING OF BLOCKS-----------------------

    /// <summary>Instantiates all the game objects to create the child piece (i.e. T, L, etc.)</summary>
    /// <param name="vectors">Vectors is a set of initial positions where the blocks will be spawned</param>
    public void SpawnBlocks(Vector2Int[] vectors)
    {
        _pieceType = GetPieceType();
        int numBlocks = vectors.Length;
        _blocks = new GameObject[numBlocks];
        gameObject.transform.parent = GameObject.Find($"PieceController_{_blockGrid.getPlayerID()}").transform; // Set the parent of the piece to the grid attached to the player controlling it

        // Iterate through each block, instantiating it, asigning a parent, making it active, initalizing it in array, and initalizing position
        for (int i = 0; i < numBlocks; i++)
        {
            //Debug.Log($"Spawning block {i} at {vectors[i]}");
            GameObject block = Instantiate(_blockPrefab);
            Block blockScript = block.GetComponent<Block>();
            blockScript.SetGrid(_blockGrid.gameObject);

            block.name = $"Block{i}";
            blockScript.SetBlockStatus(true);
            block.transform.parent = transform;
            _blocks[i] = block;
            _blocks[i].GetComponent<Block>().InitializePosition((int)vectors[i].x, (int)vectors[i].y, i);
        }
    }

    /// <summary>Checks if the blocks can be placed in the given positions. This is used to check if the blocks can be moved or rotated.</summary>
    /// <param name="positions">Set of vectors representing positons to be checked</param>
    /// <returns>Return true if valid spots, false otherwise</returns>
    public bool CheckBlockLocations(Vector2Int[] positions)
    {
        foreach (Vector2Int position in positions)
        {
            if (_blockGrid.CheckSpace((int)position.x, (int)position.y) == false)
                return false;
        }
        return true;
    }

    /// <summary>Test if the piece can be moved to the new position without actually moving it.</summary>
    public bool TestOffset(Vector2Int offset)
    {
        Vector2Int[] newPositions = CreateOffsetVectors((int)offset.x, (int)offset.y, _positions);
        if (CheckBlockLocations(newPositions))
        {
            return true;
        }
        return false;
    }

    // -----------------------MOVING BLOCKS-----------------------

    public void HardDrop()
    {
        while (true)
        {
            if (!TryMovePiece(new Vector2Int(1, 0))) break;
        }
    }

    /// <summary>Attempt to move the piece (in array and unity).</summary>'
    /// <param name="offset">Offset vector to be applied to the piece</param>
    /// <returns>True if the move was successful, false otherwise</returns>
    public bool TryMovePiece(Vector2Int offset)
    {
        Vector2Int[] newPositions = CreateOffsetVectors((int)offset.x, (int)offset.y, _positions);
        if (CheckBlockLocations(newPositions))
        {
            OffsetBlocks((int)offset.x, (int)offset.y);
            return true;
        }
        return false;
    }

    // -----------------------ROTATING BLOCKS-----------------------

    public bool TryRotateCW()
    {
        return TryRotate(true);
    }
    public bool TryRotateCCW()
    {
        return TryRotate(false);
    }

    // -----------------------PUBLIC AND PROTECTED GETTERS-----------------------

    /// <summary>Simple setter method to hardset the positions of the blocks. Doesn't change the block locations in the grid.
    /// Generally only used for initialization of the piece.</summary>
    /// <param name="positions">Initial position vectors</param>
    public void SetPositions(Vector2Int[] positions)
    {
        _positions = positions;
    }

    public virtual PieceType PieceType { get; }

    /// <summary>Returns the inital position of the piece. Simply overriden in each child piece to define starting locations.</summary>
    public virtual Vector2Int[] GetInitialPositions()
    {
        return new Vector2Int[0]; // default to empty
    }

    public virtual PieceType GetPieceType()
    {
        return PieceType;
    }

    protected virtual Vector2Int[] GetRotatedPositions(int stateFrom, bool isClockwise)
    {
        return new Vector2Int[0]; // default to empty
    }

    // -----------------------MOVING BLOCKS HELPERS-----------------------

    /// <summary>Offsets the blocks in the grid and updates their positions (in Unity and the array). This is used when the blocks are moved.</summary>
    private void OffsetBlocks(int offsetX, int offsetY)
    {
        NullGridLocations();
        for (int i = 0; i < _blocks.Length; i++)
        {
            _positions[i].x += offsetX;
            _positions[i].y += offsetY;
            _blockGrid.SetBlockInGridArray(_blocks[i], (int)_positions[i].x, (int)_positions[i].y);
            _blocks[i].GetComponent<Block>().ChangeUnityPosition((int)_positions[i].x, (int)_positions[i].y);
        }
    }
    private void AssignNewLocations(Vector2Int[] newPositions)
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            _positions[i] = newPositions[i];
            _blockGrid.SetBlockInGridArray(_blocks[i], (int)_positions[i].x, (int)_positions[i].y);
            _blocks[i].GetComponent<Block>().ChangeUnityPosition((int)_positions[i].x, (int)_positions[i].y);
        }
    }

    /// <summary>Creates a new set of vectors based on the current positions of the blocks and the given offsets. Remeber coordinate system is based on array logic (top left is 0,0), not unity coordinates.</summary>
    /// <param name="offsetX">deltaX</param>
    /// <param name="offsetY">deltaY</param>
    /// <returns>Returns the new offseted vectors as an array</returns>
    private Vector2Int[] CreateOffsetVectors(int offsetX, int offsetY, Vector2Int[] positions)
    {
        Vector2Int[] returnVectors = new Vector2Int[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            returnVectors[i] = new Vector2Int(positions[i].x + offsetX, positions[i].y + offsetY);
        }
        return returnVectors;
    }

    // -----------------------ROTATING BLOCKS HELPERS-----------------------

    private bool TryRotate(bool isClockwise)
    {


        //PieceController.PrintVector2Array(_positions);
        Vector2Int[][] rotatedPositions = new Vector2Int[5][];
        rotatedPositions[0] = CreateOffsetRotationVectors(isClockwise, _positions);

        Vector2Int[] SRSMoveOffset = KickTableManager.GetSRSKicks(_pieceType, _currentRotation, isClockwise);
        //PieceController.PrintVector2Array(SRSMoveOffset);
        for (int i = 1; i < SRSMoveOffset.Length; i++) // For each SRSKick, add the offset to the original vector and rotatae
        {
            rotatedPositions[i] = new Vector2Int[_blocks.Length];
            rotatedPositions[0].CopyTo(rotatedPositions[i], 0);
            PieceController.PrintVector2Array(rotatedPositions[i], $"before movement at {i}");
            Debug.Log($"KICKTABLE OFFSET: {SRSMoveOffset[i].x} {SRSMoveOffset[i].y}");
            rotatedPositions[i] = CreateOffsetVectors(SRSMoveOffset[i].x, SRSMoveOffset[i].y, rotatedPositions[i]);
            PieceController.PrintVector2Array(rotatedPositions[i], $"after movement at {i}");
        }

        // Try all rotations, roatte if possible
        for (int i = 0; i < rotatedPositions.Length; i++)
        {
            if (CheckBlockLocations(rotatedPositions[i]))
            {
                Debug.Log($"Rotating with i = {i}");
                PieceController.PrintVector2Array(rotatedPositions[i]);
                NullGridLocations();
                AssignNewLocations(rotatedPositions[i]);
                _currentRotation = (_currentRotation == 0 && !isClockwise) ? 3 : (_currentRotation + (isClockwise ? 1 : -1)) % 4;
                return true;
            }
        }
        return false;
    }

    /// <summary>Creates a new set of rotated vectors based on the current positions of the blocks and whether the rotation is CW or CCW.</summary>
    private Vector2Int[] CreateOffsetRotationVectors(bool isClockwise, Vector2Int[] positions) {
        Vector2Int[] rotatedOffsets = GetRotatedPositions(_currentRotation, isClockwise);
        Vector2Int[] rotatedPositions = new Vector2Int[_blocks.Length];
        for (int i = 0; i < _blocks.Length; i++)
        {
            rotatedPositions[i] = new Vector2Int(positions[i].x + rotatedOffsets[i].x, positions[i].y + rotatedOffsets[i].y);
        }
        //Debug.Log($"Initial Positions: {_positions}");
        //Debug.Log($"Rotation Vectors: {rotatedOffsets}");
        return rotatedPositions;
    }

    // -----------------------NULL BLOCKS HELPERS-----------------------

    /// <summary>Changes block references in the array grid (that this piece is using) to null.</summary>
    private void NullGridLocations()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            _blockGrid.SetBlockInGridArray(null, (int)_positions[i].x, (int)_positions[i].y);
        }
    }

    /// <summary>Sets the blocks to inactive. This is used when the blocks are no longer in play (i.e. when they are placed in the grid).</summary>
    public void SetBlocksInactive()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            //Debug.Log(i);
            //if (_blocks[i] == null)
            //    Debug.Log("is null");
            //else
            //    Debug.Log($"Setting block at {_blocks[i].GetComponent<Block>().GetPosition()} to inactive");
            _blocks[i].GetComponent<Block>().SetBlockStatus(false);
        }
    }

}