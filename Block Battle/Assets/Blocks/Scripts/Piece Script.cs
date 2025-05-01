using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Piece_Script : MonoBehaviour
{
    [SerializeField] protected GameObject _blockPrefab;
    [SerializeField] protected GameObject _grid;

    protected BlockGrid _blockGrid;
    protected GameObject[] _blocks;
    
    public Vector2[] _positions;

    protected virtual void Start()
    {
        _blockGrid = _grid.GetComponent<BlockGrid>();
        Debug.Log("Start");
        if (_blockGrid == null)
            Debug.LogWarning("NULL BLOCKGRID");
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
        bool canSpawn = CheckBlockLocations(vectors);
        if (!canSpawn)
        {
            Debug.Log("Can not spawn block");
            return;
            // Eventually this will be a condition to end the game
        }

        for (int i = 0; i < numBlocks; i++)
        {
            GameObject block = Instantiate(_blockPrefab);
            block.name = $"Block{i}";
            block.GetComponent<Block>().isActive = true;
            block.transform.parent = transform;
            _blocks[i] = block;
            _blocks[i].GetComponent<Block>().InitializePosition((int)vectors[i].x, (int)vectors[i].y);
        }
    }

    public void moveBlocks(int x, int y)
    {
        for (int i = 0; i < _blocks.Length; i++) {
            Debug.Log($"Moving block {i} to {x}, {y}");
            _blocks[i].GetComponent<Block>().MoveByOffset(x, y);
        }
    }

    public bool CheckBlockLocations(Vector2[] positions)
    {
        foreach (Vector2 position in positions)
        {
            if (_blockGrid.CheckSpace((int)position.x, (int)position.y) == false || position.x > 19 || position.y > 9)
                return false;
        }
        return true;
    }

    // Offsets the given vectors returning the new vector
    // Primarily used to check if the new spots in the grid are aviable
    public Vector2[] CreateOffsetVectors(int offsetX, int offsetY)
    {
        Vector2[] returnVectors = new Vector2[_positions.Length];
        for(int i=0; i<_positions.Length; i++)
        {
            returnVectors[i] = new Vector2(_positions[i].x + offsetX, _positions[i].y + offsetY);
        }
        return returnVectors;
    }

    public void SetPositions(Vector2[] positions)
    {
        _positions = positions;
    }
 
    // Getter method returning positions of the blokcs
    public Vector2[] GetPositions()
    {
        return _positions;
    }



    
    
}
