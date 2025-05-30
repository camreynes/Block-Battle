using UnityEngine;

public class Block : MonoBehaviour
{
    private int _posx = 0;
    private int _posy = 0;

    private bool isActive;

    protected GameObject _grid;
    protected BlockGrid _blockGrid;

    // Solely for Debug
    private int _identifier;

    public void SetPos(int x, int y)
    {
        _posx = x;
        _posy = y;
    }

    public void OffsetPosition(int x, int y)
    {
        _posx += x;
        _posy += y;
        //Debug.Log($"Offsetting position by: {x}, {y} | new pos: {_posx}, {_posy}");
        gameObject.transform.position = _blockGrid.GetPosInGrid(_posx, _posy);
    }

    // Setter Methods
    public void InitializePosition(int x, int y, int identifier)
    {
        _blockGrid.SetBlockInGridArray(gameObject, x, y);
        SetPos(x, y);
        gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
        _identifier = identifier;
    }

    // Method to change piece location
    public void ChangeUnityPosition(int x, int y)
    {
        gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
        SetPos(x, y);
        //Debug.Log($"Block position changed to: {x}, {y}");
    }

    public bool GetBlockStatus()
    {
        return isActive;
    }

    

    public void SetBlockStatus(bool status)
    {
        isActive = status;
    }
    public void SetGrid(GameObject grid)
    {
        _grid = grid;
        _blockGrid = _grid.GetComponent<BlockGrid>();
    }
}
