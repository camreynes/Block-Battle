using UnityEngine;

public class Piece_Script : MonoBehaviour
{
    [SerializeField] protected GameObject _blockPrefab;
    [SerializeField] protected GameObject _grid;

    protected GameObject[] blocks; 

    // Helper method to instantiate all the game objects
    protected void SpawnBlocks(Vector2[] vectors)
    {
        /**
         * Get the number of blocks from the amount of positions passed
         * Initialize objects
         * Attempt to Spawn them
         */
        int numBlocks = vectors.Length;
        blocks = new GameObject[numBlocks];
        bool canSpawn = attemptSpawnBlocks(vectors);
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
            blocks[i] = block;
            blocks[i].GetComponent<Block>().initializePosition(_grid.GetComponent<BlockGrid>(), (int)vectors[i].x, (int)vectors[i].y);
        }
    }

    protected bool attemptSpawnBlocks(Vector2[] positions)
    {
       foreach (Vector2 position in positions)
       {
            GameObject block = _grid.GetComponent<BlockGrid>().blocksInGrid[(int)(19-position.x), (int)position.y];
            if (block != null)
                return false;
       }
       return true;
    }

    //// Helper method to place a block and update grid
    //protected void SetBlockPosition(int index, BlockGrid grid, int row, int col)
    //{
    //    _blocks[index].transform.position = BlockGrid.PlaceObjectInGrid(grid, _blocks[index], row, col);
    //    _blocks[index].GetComponent<Block>().setPos(row, col);
    //}
}
