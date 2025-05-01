using UnityEngine;

public class Block : MonoBehaviour
{
    private int _posx = 0;
    private int _posy = 0;

    public bool isActive;

    public void setPos(int x, int y)
    {
        _posx = x;
        _posy = y;
    }

    public void initializePosition(BlockGrid grid, int x, int y)
    {
        grid.blocksInGrid[19 - _posx, _posy] = gameObject;
        setPos(x, y);
    }

    // Method to update the position and also update the blockgrid aswell
    public void updatePosition(BlockGrid grid, int x, int y)
    {
        grid.blocksInGrid[19 - _posx, _posy] = null;
        setPos(x, y);
        grid.blocksInGrid[19 - _posx, _posy] = gameObject;
    }

    // Method to offset the position and also update the blockgrid aswell, assumes checkSpace already called
    public void offsetPosition(BlockGrid grid, int offSetX, int offSetY)
    {
        updatePosition(grid, _posx + offSetX, _posy + offSetY);
    }

    // Method to check if space is avaiable based on offset
    public bool checkSpace(BlockGrid grid, int offSetX, int offSetY)
    {
        GameObject objectAtLocation = grid.blocksInGrid[_posx + offSetX, _posy + offSetY];
        if (objectAtLocation == null || objectAtLocation.GetComponent<Block>().isActive)
        {
            return true;
        }
        return false;
    }
}
