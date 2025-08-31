using TMPro;
using UnityEngine;

// THIS CLASS HAS ZERO RELATION TO HOLD STATE
public class Hold : MonoBehaviour
{

    GameObject _heldPiece;
    [SerializeField] private GameObject[] _pieces;

    float _width;
    float _height;
    float centX;
    float centY;

    int _type = -1; // Current piece type being held
    SpriteRenderer spr;
    private Sprite[] _sprites;

    private TMP_Text _worldText;

    /// <summary>
    /// Initializes the Hold component by finding center for future use.
    /// Sets up sprites, similar to Preview.cs
    /// </summary>
    public void InitializeSelf()
    {
        // SpriteRenderer bounds setup
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Vector2 size = spriteRenderer.size;
        _width = size.x;
        _height = size.y;

        centX = _width / 2f;
        centY = _height / 2f;

        _heldPiece = new GameObject("heldPiece");
        _heldPiece.transform.SetParent(transform, false);
        _heldPiece.transform.localPosition = new Vector3(-centX, -centY, 2);
        _heldPiece.SetActive(true);
        spr = _heldPiece.AddComponent<SpriteRenderer>();
        spr.GetComponent<SpriteRenderer>().sortingOrder = 3;

        _sprites = new Sprite[_pieces.Length];
        for (int i = 0; i < _pieces.Length; i++)
        {
            _sprites[i] = _pieces[i].GetComponent<SpriteRenderer>().sprite;
        }

        GameObject txt = new GameObject("HoldText");
        txt.transform.SetParent(transform, false);
        txt.transform.position = new Vector3(0, 3, 0);

        // 3. Add TextMeshPro component
        _worldText = txt.AddComponent<TextMeshPro>();

        // 4. Configure text settings
        _worldText.text = "Hello Unity!";
        _worldText.fontSize = 5;                  // bigger so visible in world
        _worldText.color = Color.yellow;
        _worldText.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// Updates the held piece. Returns the previously held type (or -1 if none).
    /// </summary>
    public int UpdateHold(int type)
    {
        // Range guard
        if (type < 0 || type >= _sprites.Length)
            return -1;

        int previous = _type; // -1 if none

        _type = type;
        spr.sprite = _sprites[type];

        return previous; // caller can spawn this piece if >= 0
    }
}
