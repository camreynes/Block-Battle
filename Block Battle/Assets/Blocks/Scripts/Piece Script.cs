using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceScript : MonoBehaviour
{
    [SerializeField] protected GameObject _blockPrefab;

    protected BlockGrid _blockGrid;
    protected GameObject[] _blocks;

    private Vector2[] _positions;

    public void SetGrid(BlockGrid grid)
    {
        _blockGrid = grid;
    }

    // Helper method to instantiate all the game objects  
    public void SpawnBlocks(Vector2[] vectors)
    {
        /**  
         * Get the number of blocks from the amount of positions passed  
         * Initialize objects  
         * Attempt to Spawn them  
         */
        int numBlocks = vectors.Length;
        _blocks = new GameObject[numBlocks];

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
            _blocks[i].GetComponent<Block>().InitializePosition((int)vectors[i].x, (int)vectors[i].y);
        }
    }

    public bool CheckBlockLocations(Vector2[] positions)
    {
        foreach (Vector2 position in positions)
        {
            if (_blockGrid.CheckSpace((int)position.x, (int)position.y) == false)
                return false;
        }
        return true;
    }

    // Offsets the given vectors returning the new vector  
    // Primarily used to check if the new spots in the grid are aviable  
    public Vector2[] CreateOffsetVectors(int offsetX, int offsetY)
    {
        Vector2[] returnVectors = new Vector2[_positions.Length];
        for (int i = 0; i < _positions.Length; i++)
        {
            returnVectors[i] = new Vector2(_positions[i].x + offsetX, _positions[i].y + offsetY);
        }
        return returnVectors;
    }

    // Sets all blocks to inactive  
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

    public void SetPositions(Vector2[] positions)
    {
        _positions = positions;
    }


    // Method to set, clear, and offset the position of the blocks
    public void ClearLocations()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            _blockGrid.SetBlockInGridArray(null, (int)_positions[i].x, (int)_positions[i].y);
        }
    }

    public void OffsetBlocks(int offsetX, int offsetY)
    {
        ClearLocations();
        for (int i = 0; i < _blocks.Length; i++)
        {
            _positions[i].x += offsetX;
            _positions[i].y += offsetY;
            _blockGrid.SetBlockInGridArray(_blocks[i], (int)_positions[i].x, (int)_positions[i].y);
            _blocks[i].GetComponent<Block>().ChangeUnityPosition((int)_positions[i].x, (int)_positions[i].y);
        }
    }


    //// Method to update the position and also update the blockgrid aswell
    //public void UpdatePosition(int x, int y)
    //{
    //    _blockGrid.SetBlockInGridArray(null, _posx, _posy);
    //    gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
    //    SetPos(x, y);
    //    _blockGrid.SetBlockInGridArray(gameObject, x, y);
    //}

    //// Method to offset the position and also update the blockgrid aswell, assumes checkSpace already called
    //public void MoveByOffset(int offSetX, int offSetY)
    //{
    //    UpdatePosition(_posx + offSetX, _posy + offSetY);
    //}

    // Getter method returning positions of the blokcs
    public virtual Vector2[] GetPositions()
    {
        return _positions;
    }

    // Overridable method to get the initial positions of the blocks
    public virtual Vector2[] GetInitialPositions()
    {
        return new Vector2[0]; // default to empty
    }
}