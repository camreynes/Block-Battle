using UnityEngine;

public class Outline : MonoBehaviour
{
    [SerializeField] private GameObject _outlinePrefab;
    [SerializeField] private Sprite[] _outlineSpritePrefabs;

    private GameObject[] _outlines = new GameObject[4];
    private SpriteRenderer[] _outlineSprites = new SpriteRenderer[4];
    private BlockGrid _grid;
    private PieceType _type = PieceType.None;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitializeSelf()
    {
        for (int i = 0; i < 4; i++) // If I were to add more pieces, I would change this value to max number of pieces and pool unused pieces
        {
            GameObject obj = Instantiate(_outlinePrefab);
            obj.transform.name = $"Outline {i}";
            obj.transform.SetParent(transform);
            _outlines[i] = obj;
            _outlineSprites[i] = obj.GetComponent<SpriteRenderer>();
        }
    }

    public void UpdateOutline(Vector2Int[] positions, PieceType type)
    {
        print(positions.Length);
        if (type != _type) {
            ChangeSprites((int)type);
            _type = type;
        }
        for (int i = 0; i < 4; i++)
        {
            float x = _grid.GetPosInGrid(positions[i].x, positions[i].y).x;
            float y = _grid.GetPosInGrid(positions[i].x, positions[i].y).y;
            _outlines[i].transform.position = new Vector2(x, y);
        }
    }

    private void ChangeSprites(int spriteNum)
    {
        for (int i = 0; i < 4; i++)
        {
            _outlineSprites[i].sprite = _outlineSpritePrefabs[spriteNum];
        }
    }


    public void SetGrid(BlockGrid grid) { _grid = grid; }
}
