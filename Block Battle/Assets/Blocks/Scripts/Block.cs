using UnityEngine;

public class Block : MonoBehaviour
{
    private int _posx = 0;
    private int _posy = 0;

    public bool isActive;

    [SerializeField] protected GameObject _grid;
    protected BlockGrid _blockGrid;

    private void Awake()
    {
        _blockGrid = _grid.GetComponent<BlockGrid>();
    }

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

    // Method to update the position and also update the blockgrid aswell
    public void UpdatePosition(int x, int y)
    {
        _blockGrid.SetBlockInGridArray(null, x, y);
        gameObject.transform.position = _blockGrid.GetPosInGrid(x, y);
        SetPos(x, y);
        _blockGrid.SetBlockInGridArray(gameObject, x, y);
    }

    // Method to offset the position and also update the blockgrid aswell, assumes checkSpace already called
    public void MoveByOffset(int offSetX, int offSetY)
    {
        UpdatePosition(_posx + offSetX, _posy + offSetY);
    }

    
}
