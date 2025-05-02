using UnityEngine;

public class Block : MonoBehaviour
{
    private int _posx = 0;
    private int _posy = 0;

    private bool isActive;

    protected GameObject _grid;
    protected BlockGrid _blockGrid;

    public void SetPos(int x, int y)
    {
        _posx = x;
        _posy = y;
    }

    public void InitializePosition(int x, int y)
    {
        _blockGrid.SetBlockInGridArray(gameObject, x, y);
        SetPos(x, y);
        gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
    }

    // Method to change piece location
    public void ChangeUnityPosition(int x, int y)
    {
        gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
        SetPos(x, y);
        Debug.Log($"Block position changed to: {x}, {y}");
    }

    public bool GetBlockStatus()
    {
        return isActive;
    }
    public Vector2 GetPosition()
    {
        return new Vector2(_posx, _posy);
    }

    // Setter Methods

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
