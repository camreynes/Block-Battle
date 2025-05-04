using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceScript : MonoBehaviour
{
    [SerializeField] protected GameObject _blockPrefab;

    protected BlockGrid _blockGrid;
    protected GameObject[] _blocks;

    private Vector2Int[] _positions;
    private int _currentRotation = 0; // 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees

    public virtual PieceType PieceType { get; }

    public void SetGrid(BlockGrid grid)
    {
        Debug.Log($"Setting grid to {grid.gameObject.name}");
        _blockGrid = grid;
    }

    /// <summary>Instantiates all the game objects to create the child piece (i.e. T, L, etc.)</summary>
    /// <param name="vectors">Vectors is a set of initial positions where the blocks will be spawned</param>
    public void SpawnBlocks(Vector2Int[] vectors)
    {
        int numBlocks = vectors.Length;
        _blocks = new GameObject[numBlocks];
        gameObject.transform.parent = GameObject.Find($"PieceController_{_blockGrid.getPlayerID()}").transform; // Set the parent of the piece to the grid attached to the player controlling it

        // Iterate through each block, instantiating it, asigning a parent, making it active, initalizing it in array, and initalizing position
        for (int i = 0; i < numBlocks; i++)
        {
            Debug.Log($"Spawning block {i} at {vectors[i]}");
            GameObject block = Instantiate(_blockPrefab);
            Block blockScript = block.GetComponent<Block>();
            blockScript.SetGrid(_blockGrid.gameObject);

            block.name = $"Block{i}";
            blockScript.SetBlockStatus(true);
            block.transform.parent = transform;
            _blocks[i] = block;
            _blocks[i].GetComponent<Block>().InitializePosition((int)vectors[i].x, (int)vectors[i].y,i);
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

    /// <summary>Creates a new set of vectors based on the current positions of the blocks and the given offsets. Remeber coordinate system is based on array logic (top left is 0,0), not unity coordinates.</summary>
    /// <param name="offsetX">deltaX</param>
    /// <param name="offsetY">deltaY</param>
    /// <returns>Returns the new offseted vectors as an array</returns>
    public Vector2Int[] CreateOffsetVectors(int offsetX, int offsetY)
    {
        Vector2Int[] returnVectors = new Vector2Int[_positions.Length];
        for (int i = 0; i < _positions.Length; i++)
        {
            returnVectors[i] = new Vector2Int(_positions[i].x + offsetX, _positions[i].y + offsetY);
        }
        return returnVectors;
    }

    /// <summary>Attempt to move the piece (in array and unity).</summary>'
    /// <param name="offset">Offset vector to be applied to the piece</param>
    /// <returns>True if the move was successful, false otherwise</returns>
    public bool TryMovePiece(Vector2Int offset)
    {
        Vector2Int[] newPositions = CreateOffsetVectors((int)offset.x, (int)offset.y);
        if (CheckBlockLocations(newPositions))
        {
            OffsetBlocks((int)offset.x, (int)offset.y);
            return true;
        }
        return false;
    }

    public bool TryRotateCW()
    {
        return TryRotate(true);
    }
    public bool TryRotateCCW()
    {
        return TryRotate(false);
    }

    private bool TryRotate(bool clockwise)
    {
        Vector2Int[] rotatedOffsets = GetRotatedPositions(_currentRotation, clockwise);
        Vector2Int[] rotatedPositions = new Vector2Int[_blocks.Length];

        Debug.Log($"Initial Positions: {_positions}");
        Debug.Log($"Rotation Vectors: {rotatedOffsets}");
        for (int i = 0; i < _blocks.Length; i++)
        {
            if (clockwise)
            {
                rotatedPositions[i] = _positions[i] + rotatedOffsets[i]; // Add the vectors
            } else
            {
                rotatedPositions[i] = _positions[i] - rotatedOffsets[i]; // Subtract the vectors (revert direction)
            }
           
        }
        Debug.Log($"New Rotations: {rotatedPositions}");

        if (CheckBlockLocations(rotatedPositions))
        {
            NullGridLocations();
            AssignNewLocations(rotatedPositions);
            Debug.Log($"Old State:{_currentRotation}");
            if (_currentRotation == 0 && !clockwise)
            {
                _currentRotation = 3;
            }
            else
            {
                _currentRotation = (_currentRotation + (clockwise ? 1 : -1)) % 4; // Increment the rotation state
            }

                Debug.Log($"New State:{_currentRotation}");
            return true;
        }
        return false;
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

    /// <summary>Simple setter method to hardset the positions of the blocks. Doesn't change the block locations in the grid.
    /// Generally only used for initialization of the piece.</summary>
    /// <param name="positions">Initial position vectors</param>
    public void SetPositions(Vector2Int[] positions)
    {
        _positions = positions;
    }


    /// <summary>Changes block references in the array grid (that this piece is using) to null.</summary>
    private void NullGridLocations()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            _blockGrid.SetBlockInGridArray(null, (int)_positions[i].x, (int)_positions[i].y);
        }
    }

    /// <summary>Offsets the blocks in the grid and updates their positions (in Unity and the array). This is used when the blocks are moved.</summary>
    public void OffsetBlocks(int offsetX, int offsetY)
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

    /// <summary>Returns the current positions of the blocks.</summary>
    public virtual Vector2Int[] GetPositions()
    {
        return _positions;
    }

    /// <summary>Returns the inital position of the piece. Simply overriden in each child piece to define starting locations.</summary>
    public virtual Vector2Int[] GetInitialPositions()
    {
        return new Vector2Int[0]; // default to empty
    }

    protected virtual Vector2Int[] GetRotatedPositions(int stateFrom, bool isClockwise)
    {
        return new Vector2Int[0]; // default to empty
    }
}